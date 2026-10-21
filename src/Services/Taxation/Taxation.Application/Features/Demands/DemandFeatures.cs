using FluentValidation;
using Microsoft.EntityFrameworkCore;

/// Computes the year's demands as drafts for every active file (all circles, or one). A computed run of the same scope is
/// replaced; files whose demand of the year is already issued are skipped.
public sealed record ComputeDemandRunCommand(int FiscalYear, Guid? CircleId) : ICommand<Result<ComputeRunResult>>;

/// Issues the run's drafts: earlier years' unpaid balances are carried onto them, the first notices are issued with
/// their SMS, and every reference is re-synced.
public sealed record ApproveDemandRunCommand(Guid Id) : ICommand<Result<ApproveRunResult>>;

public sealed record CancelDemandRunCommand(Guid Id) : ICommand<Result<UpdatedResult>>;

public sealed record GetDemandRunsQueryResult(IReadOnlyList<DemandRunDto> Runs);
public sealed record GetDemandRunsQuery(int? FiscalYear, DemandRunStatus? Status = null) : IQuery<Result<GetDemandRunsQueryResult>>;
public sealed record GetDemandRunQueryResult(DemandRunDto Run, IReadOnlyList<DemandSummaryDto> Demands);
public sealed record GetDemandRunQuery(Guid Id) : IQuery<Result<GetDemandRunQueryResult>>;

/// The files of a run's scope that have no demand of the year and could not be worked out, each with why - worked out
/// again now (nothing is saved), so the list follows the fixes made since the run was computed.
public sealed record GetDemandRunProblemsQueryResult(IReadOnlyList<RunProblemDto> Problems);
public sealed record GetDemandRunProblemsQuery(Guid Id) : IQuery<Result<GetDemandRunProblemsQueryResult>>;

public sealed record GetDemandsQueryResult(PaginatedResult<DemandSummaryDto> Demands);
public sealed record GetDemandsQuery(PaginationRequest Pagination, int? FiscalYear, Guid? CircleId, DemandStatus? Status, DemandStage? Stage, Guid? RunId, Guid? TaxFileId,
  string? Search, bool? WithBalance, bool? Overdue = null, bool? NoticeDue = null, bool? YearEndDue = null) : IQuery<Result<GetDemandsQueryResult>>;
public sealed record GetDemandQueryResult(DemandDto Demand);
public sealed record GetDemandQuery(Guid Id) : IQuery<Result<GetDemandQueryResult>>;

public class ComputeDemandRunCommandValidator : AbstractValidator<ComputeDemandRunCommand>
{
  public ComputeDemandRunCommandValidator() => RuleFor(x => x.FiscalYear).InclusiveBetween(FiscalYear.Earliest, FiscalYear.Latest);
}

public class DemandRunHandlers(
  IApplicationDbContext context,
  TaxSetup setup,
  Assessor assessor,
  Ledger ledger,
  CodeIssuer codes,
  NoticeComposer notices,
  Messenger messenger,
  PenaltyService penalties,
  ICurrentUser currentUser,
  IClock clock) :
  ICommandHandler<ComputeDemandRunCommand, Result<ComputeRunResult>>,
  ICommandHandler<ApproveDemandRunCommand, Result<ApproveRunResult>>,
  ICommandHandler<CancelDemandRunCommand, Result<UpdatedResult>>,
  IQueryHandler<GetDemandRunsQuery, Result<GetDemandRunsQueryResult>>,
  IQueryHandler<GetDemandRunQuery, Result<GetDemandRunQueryResult>>,
  IQueryHandler<GetDemandRunProblemsQuery, Result<GetDemandRunProblemsQueryResult>>
{
  public async Task<Result<ComputeRunResult>> Handle(ComputeDemandRunCommand command, CancellationToken cancellationToken)
  {
    var fy = command.FiscalYear;
    TaxCircleId? circleId = command.CircleId is { } c ? (await context.LoadCircleAsync(c, cancellationToken)).Id : null;

    // the year-end surcharges of earlier years first, so the balances carried are final
    await penalties.ApplyYearEndAsync(cancellationToken);

    var ctx = await assessor.ContextAsync(fy, cancellationToken);
    var run = DemandRun.Compute(DemandRunId.New(), fy, circleId, ctx.RateSet, currentUser.UserId, clock.UtcNow);
    context.DemandRuns.Add(run);

    // a computed run of the same scope is replaced
    foreach (var old in await context.DemandRuns.Where(r => r.FiscalYear == fy && r.CircleId == circleId && r.Status == DemandRunStatus.Computed).ToListAsync(cancellationToken))
      old.Cancel();

    var files = await context.TaxFilesWithDetails().Where(f => f.TaxStatus == TaxStatus.Active && (circleId == null || f.CircleId == circleId)).ToListAsync(cancellationToken);
    var fileIds = files.Select(f => f.Id).ToList();
    var existing = (await context.DemandsWithItems().Where(d => d.FiscalYear == fy && fileIds.Contains(d.TaxFileId) && d.Status != DemandStatus.Cancelled).ToListAsync(cancellationToken))
      .ToDictionary(d => d.TaxFileId);

    var computed = 0;
    var skipped = 0;
    var held = 0;
    var total = 0m;
    var problems = new List<RunProblemDto>();
    var latestSync = DateTime.MinValue;
    var yearStart = FiscalYear.Start(fy);

    foreach (var file in files.OrderBy(f => f.FileNo))
    {
      existing.TryGetValue(file.Id, out var demand);
      if (demand is not null && demand.Status != DemandStatus.Draft)
      {
        skipped++;
        continue;
      }

      try
      {
        var assessment = Assessor.Assess(ctx, file);
        if (assessment.Suspended)
        {
          if (demand is not null)
            context.Demands.Remove(demand);
          held++;
          continue;
        }

        var basis = file.CurrentBasis!;
        var payer = file.Addressee(yearStart) ?? file.Addressee(clock.Today)
          ?? throw new DomainException("Property has no owner, lessee, tenant or allottee for it. Record one there and sync the file.");
        var arrears = await ledger.ArrearsPreviewAsync(file, fy, cancellationToken);

        if (demand is null)
        {
          demand = Demand.Draft(DemandId.New(), await codes.DemandNoAsync(fy, cancellationToken), run.Id, file, basis, payer, fy, assessment.Lines, arrears);
          context.Demands.Add(demand);
        }
        else
        {
          demand.Recompute(run.Id, basis, payer, assessment.Lines, arrears);
        }

        computed++;
        total += demand.Balance;
        if (basis.SyncedAt > latestSync)
          latestSync = basis.SyncedAt;
      }
      catch (DomainException ex)
      {
        problems.Add(new RunProblemDto(file.Id.Value, file.FileNo, DomainMessages.Text(ex)));
      }
    }

    run.Record(computed, total, problems.Count, latestSync == DateTime.MinValue ? clock.UtcNow : latestSync);
    await context.SaveChangesAsync(cancellationToken);
    return Result<ComputeRunResult>.Success(new(run.Id.Value, computed, skipped, held, total, problems));
  }

  public async Task<Result<ApproveRunResult>> Handle(ApproveDemandRunCommand command, CancellationToken cancellationToken)
  {
    var run = await context.LoadRunAsync(command.Id, cancellationToken);
    var actor = Guard.Actor(currentUser.UserId, "approve a demand run");
    var today = clock.Today;
    run.Approve(actor, clock.UtcNow, today);

    var yearEnds = await penalties.ApplyYearEndAsync(cancellationToken);
    var settings = await setup.SettingsAsync(today, cancellationToken);
    var surcharge = await setup.HeadAsync(HeadCodes.Surcharge, cancellationToken);
    var drafts = await context.DemandsWithItems().Where(d => d.RunId == run.Id && d.Status == DemandStatus.Draft).ToListAsync(cancellationToken);
    var fileIds = drafts.Select(d => d.TaxFileId).ToList();
    var files = (await context.TaxFilesWithDetails().Where(f => fileIds.Contains(f.Id)).ToListAsync(cancellationToken)).ToDictionary(f => f.Id);
    var stale = await StaleAsync(run, drafts, files, cancellationToken);

    var noticesIssued = 0;
    var sms = 0;
    var credits = 0m;
    var held = new List<RunProblemDto>();
    foreach (var demand in drafts)
    {
      var file = files[demand.TaxFileId];
      if (file.Addressee(today) is { } payer)
        demand.Address(payer);

      var billed = await ledger.CarryIntoAsync(demand, surcharge.Id, actor, clock.UtcNow, today, cancellationToken);
      demand.Issue(today, settings.DueDate(demand.FiscalYear, today), billed);
      credits += await ledger.MoveCreditsAsync(file.Id, surcharge.Id, today, cancellationToken);

      if (demand.Balance > 0)
      {
        if (file.HoldOn(today) is { } hold)
        {
          // a hold put on after the run was worked out: the demand stands, its notice waits for the hold to end
          held.Add(new RunProblemDto(file.Id.Value, file.FileNo, $"On litigation hold since {hold.StartedOn:dd MMM yyyy}: no first notice while it is open."));
        }
        else
        {
          var notice = await notices.PrepareAsync(demand, file, NoticeStage.First, NoticeOrigin.Cycle, today, null, cancellationToken);
          await notices.ConfirmAsync(notice, demand, file, actor, cancellationToken);
          noticesIssued++;
          if (notice.SmsMessageId is not null)
            sms++;
        }
      }
      await messenger.SyncAsync(file, PsidSyncReason.AnnualDemand, demand.Id, null, demand.DueDate, $"FY {FiscalYear.Label(demand.FiscalYear)} demand {demand.DemandNo}", cancellationToken);
    }

    await context.SaveChangesAsync(cancellationToken);
    return Result<ApproveRunResult>.Success(new(run.Id.Value, drafts.Count, noticesIssued, sms, yearEnds, held, stale, credits));
  }

  /// Drafts whose file changed after the run was worked out: the computation now gives another amount, the file is now on
  /// hold or not assessable, or its Property data was synced since.
  private async Task<List<RunProblemDto>> StaleAsync(DemandRun run, IReadOnlyList<Demand> drafts, IReadOnlyDictionary<TaxFileId, TaxFile> files,
      CancellationToken cancellationToken)
  {
    var stale = new List<RunProblemDto>();
    if (drafts.Count == 0)
      return stale;
    AssessmentContext ctx;
    try
    {
      ctx = await assessor.ContextAsync(run.FiscalYear, cancellationToken);
    }
    catch (DomainException ex)
    {
      return [new RunProblemDto(Guid.Empty, "", DomainMessages.Text(ex))];
    }
    if (ctx.RateSet.Id != run.RateSetId)
      stale.Add(new RunProblemDto(Guid.Empty, "", $"The rate set in force on 1 July is now '{ctx.RateSet.Name}', not the one the run was worked out with."));
    foreach (var demand in drafts)
    {
      if (!files.TryGetValue(demand.TaxFileId, out var file))
      {
        stale.Add(new RunProblemDto(demand.TaxFileId.Value, demand.DemandNo, "The file is no longer active; its draft would still be issued."));
        continue;
      }
      try
      {
        var fresh = Assessor.Assess(ctx, file);
        if (fresh.Suspended)
          stale.Add(new RunProblemDto(file.Id.Value, file.FileNo, $"Not assessed now ({fresh.SuspendedReason})"));
        else if (fresh.Total != demand.CurrentAmount)
          stale.Add(new RunProblemDto(file.Id.Value, file.FileNo, $"Worked out again now: {Rupees.Text(fresh.Total)} instead of {Rupees.Text(demand.CurrentAmount)}"));
        else if (file.CurrentBasis is { } basis && basis.Id != demand.BasisId)
          stale.Add(new RunProblemDto(file.Id.Value, file.FileNo, $"Property data synced on {basis.SyncedAt.AddHours(5):dd MMM yyyy} after the run (same amount)"));
      }
      catch (DomainException ex)
      {
        stale.Add(new RunProblemDto(file.Id.Value, file.FileNo, DomainMessages.Text(ex)));
      }
    }
    return stale;
  }

  public async Task<Result<UpdatedResult>> Handle(CancelDemandRunCommand command, CancellationToken cancellationToken)
  {
    var run = await context.LoadRunAsync(command.Id, cancellationToken);
    run.Cancel();
    var drafts = await context.DemandsWithItems().Where(d => d.RunId == run.Id && d.Status == DemandStatus.Draft).ToListAsync(cancellationToken);
    foreach (var draft in drafts)
      context.Demands.Remove(draft);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<GetDemandRunsQueryResult>> Handle(GetDemandRunsQuery query, CancellationToken cancellationToken)
  {
    var runs = await context.DemandRuns.AsNoTracking().Where(r => (query.FiscalYear == null || r.FiscalYear == query.FiscalYear) && (query.Status == null || r.Status == query.Status))
      .OrderByDescending(r => r.ComputedAt).ToListAsync(cancellationToken);
    return Result<GetDemandRunsQueryResult>.Success(new(await DtosAsync(runs, cancellationToken)));
  }

  public async Task<Result<GetDemandRunQueryResult>> Handle(GetDemandRunQuery query, CancellationToken cancellationToken)
  {
    var run = await context.LoadRunAsync(query.Id, cancellationToken);
    var demands = await context.Demands.AsNoTracking().Where(d => d.RunId == run.Id).OrderBy(d => d.DemandNo).ToListAsync(cancellationToken);
    var fileIds = demands.Select(d => d.TaxFileId).ToList();
    var files = await context.TaxFiles.AsNoTracking().Where(f => fileIds.Contains(f.Id)).ToDictionaryAsync(f => f.Id, f => (f.FileNo, f.Psid), cancellationToken);
    return Result<GetDemandRunQueryResult>.Success(new((await DtosAsync([run], cancellationToken))[0],
      demands.Select(d => d.ToSummary(files.GetValueOrDefault(d.TaxFileId).FileNo ?? "", files.GetValueOrDefault(d.TaxFileId).Psid)).ToList()));
  }

  public async Task<Result<GetDemandRunProblemsQueryResult>> Handle(GetDemandRunProblemsQuery query, CancellationToken cancellationToken)
  {
    var run = await context.LoadRunAsync(query.Id, cancellationToken);
    var fy = run.FiscalYear;
    var files = await context.TaxFilesWithDetails().Where(f => f.TaxStatus == TaxStatus.Active && (run.CircleId == null || f.CircleId == run.CircleId)).ToListAsync(cancellationToken);
    var fileIds = files.Select(f => f.Id).ToList();
    var withDemand = (await context.Demands.Where(d => d.FiscalYear == fy && fileIds.Contains(d.TaxFileId) && d.Status != DemandStatus.Cancelled)
      .Select(d => d.TaxFileId).ToListAsync(cancellationToken)).ToHashSet();
    var missing = files.Where(f => !withDemand.Contains(f.Id)).OrderBy(f => f.FileNo).ToList();
    // drafts of a computed run that would be issued with an amount out of date
    var staleRows = new List<RunProblemDto>();
    if (run.Status == DemandRunStatus.Computed)
    {
      var drafts = await context.DemandsWithItems().AsNoTracking().Where(d => d.RunId == run.Id && d.Status == DemandStatus.Draft).ToListAsync(cancellationToken);
      staleRows = await StaleAsync(run, drafts, files.ToDictionary(f => f.Id), cancellationToken);
    }
    if (missing.Count == 0)
      return Result<GetDemandRunProblemsQueryResult>.Success(new(staleRows));

    var ctx = await assessor.ContextAsync(fy, cancellationToken);
    var yearStart = FiscalYear.Start(fy);
    var problems = new List<RunProblemDto>();
    foreach (var file in missing)
    {
      try
      {
        var assessment = Assessor.Assess(ctx, file);
        if (assessment.Suspended)
          continue;
        if (file.Addressee(yearStart) is null && file.Addressee(clock.Today) is null)
          throw new DomainException("Property has no owner, lessee, tenant or allottee for it. Record one there and sync the file.");
        problems.Add(new RunProblemDto(file.Id.Value, file.FileNo, "Can be worked out now - compute the run again to include it."));
      }
      catch (DomainException ex)
      {
        problems.Add(new RunProblemDto(file.Id.Value, file.FileNo, DomainMessages.Text(ex)));
      }
    }
    return Result<GetDemandRunProblemsQueryResult>.Success(new(problems.Concat(staleRows).ToList()));
  }

  private async Task<List<DemandRunDto>> DtosAsync(IReadOnlyList<DemandRun> runs, CancellationToken cancellationToken)
  {
    var circles = await context.TaxCircles.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Code, cancellationToken);
    var sets = await context.RateSets.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Name, cancellationToken);
    var ids = runs.Select(r => r.Id).ToList();
    var drafts = await context.Demands.Where(d => d.RunId != null && ids.Contains(d.RunId) && d.Status == DemandStatus.Draft)
      .GroupBy(d => d.RunId).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(cancellationToken);
    return runs.Select(r => new DemandRunDto(r.Id.Value, r.FiscalYear, FiscalYear.Label(r.FiscalYear), r.CircleId?.Value, r.CircleId is { } c ? circles.GetValueOrDefault(c) : null,
      r.RateSetId.Value, sets.GetValueOrDefault(r.RateSetId), r.Status, r.BasisSyncedAt, r.PropertyCount, r.TotalAmount, r.ExceptionCount, r.ComputedAt, r.ComputedBy,
      r.ApprovedAt, r.ApprovedBy, drafts.FirstOrDefault(d => d.Key == r.Id)?.Count ?? 0)).ToList();
  }
}

public class DemandQueryHandlers(IApplicationDbContext context, TaxSetup setup, Ledger ledger, DueNotices dueNotices, IClock clock) :
  IQueryHandler<GetDemandsQuery, Result<GetDemandsQueryResult>>,
  IQueryHandler<GetDemandQuery, Result<GetDemandQueryResult>>
{
  public async Task<Result<GetDemandsQueryResult>> Handle(GetDemandsQuery query, CancellationToken cancellationToken)
  {
    var rows = context.Demands.AsNoTracking().Where(d => d.Status != DemandStatus.Cancelled);
    if (query.FiscalYear is { } fy)
      rows = rows.Where(d => d.FiscalYear == fy);
    if (query.Status is { } status)
      rows = rows.Where(d => d.Status == status);
    if (query.Stage is { } stage)
      rows = rows.Where(d => d.Stage == stage);
    if (query.RunId is { } run)
    {
      var runId = DemandRunId.Of(run);
      rows = rows.Where(d => d.RunId == runId);
    }
    if (query.TaxFileId is { } file)
    {
      var fileId = TaxFileId.Of(file);
      rows = rows.Where(d => d.TaxFileId == fileId);
    }
    if (query.CircleId is { } circle)
    {
      var circleId = TaxCircleId.Of(circle);
      var inCircle = context.TaxFiles.Where(f => f.CircleId == circleId).Select(f => f.Id);
      rows = rows.Where(d => inCircle.Contains(d.TaxFileId));
    }
    if (query.WithBalance == true)
      rows = rows.Where(d => d.Balance > 0 && d.Status != DemandStatus.Draft);
    if (query.Overdue == true)
    {
      // issued, still owing, and its due date has passed
      var today = clock.Today;
      rows = rows.Where(d => (d.Status == DemandStatus.Issued || d.Status == DemandStatus.PartPaid || d.Status == DemandStatus.Defaulted)
        && d.Balance > 0 && d.DueDate != null && d.DueDate < today);
    }
    if (query.YearEndDue == true)
    {
      // a year that is over, still owing, its year-end surcharge not applied yet
      var current = FiscalYear.Of(clock.Today);
      rows = rows.Where(d => (d.Status == DemandStatus.Issued || d.Status == DemandStatus.PartPaid) && d.Balance > 0 && d.FiscalYear < current);
    }
    if (query.NoticeDue == true)
    {
      var due = await dueNotices.DemandsAsync(clock.Today, cancellationToken);
      rows = rows.Where(d => due.Contains(d.Id));
    }
    if (!string.IsNullOrWhiteSpace(query.Search))
    {
      var s = query.Search.Trim().ToUpperInvariant();
      var digits = new string(s.Where(c => !char.IsWhiteSpace(c)).ToArray());
      var files = context.TaxFiles.Where(f => f.FileNo.ToUpper().Contains(s) || (f.Psid != null && f.Psid.Contains(digits))).Select(f => f.Id);
      rows = rows.Where(d => d.DemandNo.ToUpper().Contains(s) || d.PayerName.ToUpper().Contains(s) || files.Contains(d.TaxFileId));
    }

    var total = await rows.LongCountAsync(cancellationToken);
    var page = await rows.OrderByDescending(d => d.FiscalYear).ThenBy(d => d.DemandNo)
      .Skip(query.Pagination.Pageindex * query.Pagination.PageSize).Take(query.Pagination.PageSize).ToListAsync(cancellationToken);
    var ids = page.Select(d => d.TaxFileId).Distinct().ToList();
    var fileInfo = await context.TaxFiles.AsNoTracking().Where(f => ids.Contains(f.Id)).ToDictionaryAsync(f => f.Id, f => (f.FileNo, f.Psid), cancellationToken);
    var data = page.Select(d => d.ToSummary(fileInfo.GetValueOrDefault(d.TaxFileId).FileNo ?? "", fileInfo.GetValueOrDefault(d.TaxFileId).Psid)).ToList();
    return Result<GetDemandsQueryResult>.Success(new(new PaginatedResult<DemandSummaryDto>(query.Pagination.Pageindex, query.Pagination.PageSize, total, data)));
  }

  public async Task<Result<GetDemandQueryResult>> Handle(GetDemandQuery query, CancellationToken cancellationToken)
  {
    var demand = await context.LoadDemandAsync(query.Id, cancellationToken);
    var file = await context.TaxFiles.AsNoTracking().FirstAsync(f => f.Id == demand.TaxFileId, cancellationToken);
    var heads = (await setup.HeadsAsync(cancellationToken)).ToDictionary(h => h.Id);
    var paid = await ledger.PaidByItemAsync([demand], cancellationToken);
    var notices = await context.Notices.AsNoTracking().Where(n => n.DemandId == demand.Id).ToListAsync(cancellationToken);
    return Result<GetDemandQueryResult>.Success(new(demand.ToDto(file.FileNo, file.Psid, heads, paid, notices)));
  }
}

/// A domain message without the wrapper DomainException adds, for lists of problems.
public static class DomainMessages
{
  public static string Text(DomainException exception)
  {
    const string prefix = "Domain Exception \"";
    var message = exception.Message;
    if (message.StartsWith(prefix, StringComparison.Ordinal))
    {
      var end = message.LastIndexOf(" \" throws", StringComparison.Ordinal);
      if (end > prefix.Length)
        return message[prefix.Length..end];
    }
    return message;
  }
}
