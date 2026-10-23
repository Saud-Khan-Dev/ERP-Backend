using FluentValidation;
using Microsoft.EntityFrameworkCore;

/// The two penalties: the automatic year-end surcharge (once 30 June of a demand's year is over) and the in-year
/// surcharge the Section applies to a set of properties.
public sealed class PenaltyService(
  IApplicationDbContext context,
  TaxSetup setup,
  Messenger messenger,
  IClock clock)
{
  /// Applies the year-end surcharge to every issued demand whose year is over and that still owes; the demand becomes
  /// DEFAULTED and its reference is re-synced. Idempotent: a demand takes it once. Returns how many were surcharged.
  public async Task<int> ApplyYearEndAsync(CancellationToken cancellationToken)
  {
    var today = clock.Today;
    var lastEnded = FiscalYear.Of(today) - 1;
    var candidates = await context.DemandsWithItems()
      .Where(d => (d.Status == DemandStatus.Issued || d.Status == DemandStatus.PartPaid) && d.FiscalYear <= lastEnded && d.Balance > 0)
      .ToListAsync(cancellationToken);
    if (candidates.Count == 0)
      return 0;

    var fileIds = candidates.Select(d => d.TaxFileId).Distinct().ToList();
    var files = (await context.TaxFilesWithDetails().Where(f => fileIds.Contains(f.Id)).ToListAsync(cancellationToken)).ToDictionary(f => f.Id);
    var count = 0;
    foreach (var demand in candidates)
    {
      var yearEnd = FiscalYear.End(demand.FiscalYear);
      var rule = await setup.RuleAsync(PenaltyKind.YearEnd, yearEnd, cancellationToken);
      var file = files[demand.TaxFileId];
      if (rule is null || file.HoldOn(yearEnd) is not null)
        continue;

      var penalty = demand.ApplyYearEnd(rule, today);
      if (penalty is null)
        continue;
      count++;

      var values = Messenger.Values(file, demand);
      values["pct"] = rule.RateType == PenaltyRateType.Percent ? Rupees.Number(rule.Rate) : "";
      values["surcharge"] = Rupees.Text(penalty.Amount);
      var ownerId = file.Payers.FirstOrDefault(p => p.Id == demand.PayerId)?.OwnerId;
      await messenger.SmsAsync(MessageCodes.YearEnd, file, demand, null, demand.PayerMobile, ownerId, values, cancellationToken);
      await messenger.SyncAsync(file, PsidSyncReason.PenaltyAddUp, demand.Id, penalty.Id, null,
        $"year-end surcharge {(rule.RateType == PenaltyRateType.Percent ? Rupees.Number(rule.Rate) + " %" : Rupees.Text(rule.Rate))} on FY {FiscalYear.Label(demand.FiscalYear)}", cancellationToken);
    }
    return count;
  }

  /// The demands an in-year surcharge would touch, with the base, the amount and whether each is eligible today.
  public async Task<(PenaltyRule Rule, List<(SurchargeCandidate Candidate, TaxFile File)> Rows)> CandidatesAsync(SurchargeTerms terms, CancellationToken cancellationToken)
  {
    var today = clock.Today;
    var rule = await setup.RuleAsync(PenaltyKind.IntraYear, today, cancellationToken)
      ?? throw new DomainException("No in-year surcharge rule is in force. Set it in Configuration → Surcharge rules.");
    var settings = await setup.SettingsAsync(today, cancellationToken);
    var demands = await context.DemandsWithItems()
      .Where(d => d.FiscalYear == terms.FiscalYear && (d.Status == DemandStatus.Issued || d.Status == DemandStatus.PartPaid) && d.Balance > 0)
      .ToListAsync(cancellationToken);
    var fileIds = demands.Select(d => d.TaxFileId).Distinct().ToList();
    var files = (await context.TaxFilesWithDetails().Where(f => fileIds.Contains(f.Id)).ToListAsync(cancellationToken)).ToDictionary(f => f.Id);

    var rows = new List<(SurchargeCandidate, TaxFile)>();
    foreach (var demand in demands)
    {
      var file = files[demand.TaxFileId];
      if (SurchargeRules.Candidate(demand, file, rule, terms, settings, today) is { } candidate)
        rows.Add((candidate, file));
    }
    return (rule, rows.OrderBy(r => r.Item2.FileNo).ToList());
  }
}

public sealed record SurchargeRequest(int FiscalYear, Guid? CircleId, TargetSet TargetSet, bool IncludesCurrent, PenaltyRateType RateType, decimal Rate, string? Reference);

public sealed record PreviewSurchargeQuery(SurchargeRequest Terms) : IQuery<Result<SurchargePreviewDto>>;

/// Applies an in-year surcharge to every eligible demand: a penalty on each, a surcharge notice prepared for
/// confirmation, and the reference re-synced ("add up").
public sealed record ApplySurchargeCommand(SurchargeRequest Terms) : ICommand<Result<ApplySurchargeResult>>;
public sealed record ApplySurchargeResult(Guid RunId, int Surcharged, decimal Total, int NoticesPrepared);

public sealed record ApplyYearEndCommand : ICommand<Result<ApplyYearEndResult>>;
public sealed record ApplyYearEndResult(int Surcharged);

public sealed record GetPenaltyRunsQueryResult(IReadOnlyList<PenaltyRunDto> Runs);
public sealed record GetPenaltyRunsQuery(int? FiscalYear) : IQuery<Result<GetPenaltyRunsQueryResult>>;

/// A surcharge run with the surcharges it applied and the notices it prepared.
public sealed record GetPenaltyRunQueryResult(PenaltyRunDto Run, IReadOnlyList<PenaltyListItemDto> Penalties, IReadOnlyList<NoticeListItemDto> Notices);
public sealed record GetPenaltyRunQuery(Guid Id) : IQuery<Result<GetPenaltyRunQueryResult>>;

/// Every surcharge applied (the Surcharges report): by year, kind, circle, run, file and date.
public sealed record GetPenaltiesQueryResult(PaginatedResult<PenaltyListItemDto> Penalties, decimal Total);
public sealed record GetPenaltiesQuery(PaginationRequest Pagination, int? FiscalYear, PenaltyKind? Kind, Guid? CircleId, Guid? RunId, Guid? TaxFileId, DateOnly? From,
  DateOnly? To, string? Search) : IQuery<Result<GetPenaltiesQueryResult>>;

public class SurchargeRequestValidator : AbstractValidator<SurchargeRequest>
{
  public SurchargeRequestValidator()
  {
    RuleFor(x => x.FiscalYear).InclusiveBetween(FiscalYear.Earliest, FiscalYear.Latest);
    RuleFor(x => x.Rate).GreaterThan(0);
    RuleFor(x => x.Rate).LessThanOrEqualTo(100).When(x => x.RateType == PenaltyRateType.Percent);
    RuleFor(x => x.Reference).MaximumLength(100);
  }
}

public class PreviewSurchargeQueryValidator : AbstractValidator<PreviewSurchargeQuery>
{
  public PreviewSurchargeQueryValidator() => RuleFor(x => x.Terms).NotNull().SetValidator(new SurchargeRequestValidator());
}

public class ApplySurchargeCommandValidator : AbstractValidator<ApplySurchargeCommand>
{
  public ApplySurchargeCommandValidator() => RuleFor(x => x.Terms).NotNull().SetValidator(new SurchargeRequestValidator());
}

public class PenaltyHandlers(
  IApplicationDbContext context,
  PenaltyService penalties,
  NoticeComposer notices,
  Messenger messenger,
  ICurrentUser currentUser,
  IAuditTrail audit,
  IClock clock) :
  IQueryHandler<PreviewSurchargeQuery, Result<SurchargePreviewDto>>,
  ICommandHandler<ApplySurchargeCommand, Result<ApplySurchargeResult>>,
  ICommandHandler<ApplyYearEndCommand, Result<ApplyYearEndResult>>,
  IQueryHandler<GetPenaltyRunsQuery, Result<GetPenaltyRunsQueryResult>>,
  IQueryHandler<GetPenaltyRunQuery, Result<GetPenaltyRunQueryResult>>,
  IQueryHandler<GetPenaltiesQuery, Result<GetPenaltiesQueryResult>>
{
  public async Task<Result<SurchargePreviewDto>> Handle(PreviewSurchargeQuery query, CancellationToken cancellationToken)
  {
    var (_, rows) = await penalties.CandidatesAsync(await TermsAsync(query.Terms, cancellationToken), cancellationToken);
    var list = rows.Where(r => context.Covers(r.File)).Select(r => new SurchargeCandidateDto(r.Candidate.Demand.Id.Value, r.Candidate.Demand.DemandNo, r.File.Id.Value, r.File.FileNo,
      r.Candidate.Demand.PayerName, r.Candidate.Demand.Balance, r.Candidate.ArrearsOwed, r.Candidate.Base, r.Candidate.Amount, r.Candidate.Eligible,
      r.Candidate.Reason, r.Candidate.SurchargedOn)).ToList();
    return Result<SurchargePreviewDto>.Success(new(list.Count(c => c.Eligible), list.Where(c => c.Eligible).Sum(c => c.Amount), list));
  }

  public async Task<Result<ApplySurchargeResult>> Handle(ApplySurchargeCommand command, CancellationToken cancellationToken)
  {
    var terms = await TermsAsync(command.Terms, cancellationToken);
    if (await context.CircleRefusalAsync(terms.CircleId, "apply a surcharge run", cancellationToken) is { } outside)
      return Result<ApplySurchargeResult>.Failure(outside);
    var (rule, rows) = await penalties.CandidatesAsync(terms, cancellationToken);
    var eligible = rows.Where(r => r.Candidate.Eligible).ToList();
    if (eligible.Count == 0)
      return Result<ApplySurchargeResult>.Failure($"No property is eligible for this surcharge on {clock.Today:yyyy-MM-dd}.");

    var actor = Guard.Actor(currentUser.UserId, "approve a surcharge run");
    // the surcharges, notices and syncs it writes are one line for the run
    using var quiet = audit.Quietly();
    var run = PenaltyRun.Create(PenaltyRunId.New(), rule, terms, clock.Today, actor);
    context.PenaltyRuns.Add(run);

    var prepared = 0;
    foreach (var (candidate, file) in eligible)
    {
      var demand = candidate.Demand;
      var penalty = demand.AddPenalty(rule, run.Id, candidate.Base, candidate.Amount, terms.RateType == PenaltyRateType.Percent ? terms.Rate : null, clock.Today);
      await notices.PrepareAsync(demand, file, NoticeStage.Surcharge, NoticeOrigin.Cycle, clock.Today,
        new NoticeOptions(Surcharge: penalty.Amount, PenaltyRunId: run.Id), cancellationToken);
      prepared++;
      await messenger.SyncAsync(file, PsidSyncReason.PenaltyAddUp, demand.Id, penalty.Id, null, $"surcharge run of {clock.Today:yyyy-MM-dd}", cancellationToken);
    }
    run.Record(eligible.Count, eligible.Sum(e => e.Candidate.Amount));
    audit.Record(AuditActions.Ran, run, $"Surcharged {eligible.Count} demand(s), {Rupees.Text(run.TotalAmount ?? 0)} ({(terms.RateType == PenaltyRateType.Percent ? Rupees.Number(terms.Rate) + " %" : Rupees.Text(terms.Rate))}, "
      + $"{EnumText.Words(terms.TargetSet).ToLowerInvariant()}); {prepared} surcharge notice(s) prepared");

    await context.SaveChangesAsync(cancellationToken);
    return Result<ApplySurchargeResult>.Success(new(run.Id.Value, eligible.Count, run.TotalAmount ?? 0, prepared));
  }

  public async Task<Result<ApplyYearEndResult>> Handle(ApplyYearEndCommand command, CancellationToken cancellationToken)
  {
    if (context.EveryTownRefusal("apply the year-end surcharge") is { } outside)
      return Result<ApplyYearEndResult>.Failure(outside);
    using var quiet = audit.Quietly();
    var count = await penalties.ApplyYearEndAsync(cancellationToken);
    audit.Record(AuditActions.Ran, "Year-end surcharge", null, clock.Today.ToString("yyyy-MM-dd"), null, null, null,
      $"Applied to {count} demand(s) of years that are over");
    await context.SaveChangesAsync(cancellationToken);
    return Result<ApplyYearEndResult>.Success(new(count));
  }

  public async Task<Result<GetPenaltyRunsQueryResult>> Handle(GetPenaltyRunsQuery query, CancellationToken cancellationToken)
  {
    var rows = context.PenaltyRuns.AsNoTracking().Where(r => query.FiscalYear == null || r.FiscalYear == query.FiscalYear);
    if (context.TownScope is { } towns)
    {
      var circles = context.CircleTowns.Where(t => towns.Contains(t.TownId)).Select(t => t.CircleId);
      rows = rows.Where(r => r.CircleId == null || circles.Contains(r.CircleId));
    }
    var runs = await rows.OrderByDescending(r => r.RunOn).ThenByDescending(r => r.CreatedAt).ToListAsync(cancellationToken);
    return Result<GetPenaltyRunsQueryResult>.Success(new(await RunDtosAsync(runs, cancellationToken)));
  }

  public async Task<Result<GetPenaltyRunQueryResult>> Handle(GetPenaltyRunQuery query, CancellationToken cancellationToken)
  {
    var id = PenaltyRunId.Of(query.Id);
    var run = await context.PenaltyRuns.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
      ?? throw new DemandRunNotFoundException($"Surcharge run {query.Id} was not found.");
    if (!await context.SeesRunOfAsync(run.CircleId, cancellationToken))
      throw new DemandRunNotFoundException($"Surcharge run {query.Id} was not found.");
    var penalties = await PenaltyRowsAsync(context.Penalties.AsNoTracking().InScope(context).Where(p => p.RunId == id), 0, 5000, cancellationToken);
    var notices = await context.Notices.AsNoTracking().InScope(context).Where(n => n.PenaltyRunId == id).OrderBy(n => n.NoticeNo).ToListAsync(cancellationToken);
    var demandIds = notices.Select(n => n.DemandId).Distinct().ToList();
    var demands = await context.Demands.AsNoTracking().Where(d => demandIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, cancellationToken);
    var fileIds = demands.Values.Select(d => d.TaxFileId).Distinct().ToList();
    var files = await context.TaxFiles.AsNoTracking().Where(f => fileIds.Contains(f.Id)).ToDictionaryAsync(f => f.Id, f => f.FileNo, cancellationToken);
    return Result<GetPenaltyRunQueryResult>.Success(new((await RunDtosAsync([run], cancellationToken))[0], penalties,
      notices.Select(n => n.ToListItem(demands[n.DemandId], files.GetValueOrDefault(demands[n.DemandId].TaxFileId) ?? "")).ToList()));
  }

  public async Task<Result<GetPenaltiesQueryResult>> Handle(GetPenaltiesQuery query, CancellationToken cancellationToken)
  {
    var rows = context.Penalties.AsNoTracking().InScope(context);
    if (query.Kind is { } kind)
      rows = rows.Where(p => p.Kind == kind);
    if (query.RunId is { } run)
    {
      var runId = PenaltyRunId.Of(run);
      rows = rows.Where(p => p.RunId == runId);
    }
    if (query.From is { } from)
      rows = rows.Where(p => p.AppliedOn >= from);
    if (query.To is { } to)
      rows = rows.Where(p => p.AppliedOn <= to);
    if (query.FiscalYear is not null || query.CircleId is not null || query.TaxFileId is not null || !string.IsNullOrWhiteSpace(query.Search))
    {
      var demands = context.Demands.AsQueryable();
      if (query.FiscalYear is { } fy)
        demands = demands.Where(d => d.FiscalYear == fy);
      if (query.TaxFileId is { } f)
      {
        var fileId = TaxFileId.Of(f);
        demands = demands.Where(d => d.TaxFileId == fileId);
      }
      if (query.CircleId is { } c)
      {
        var circleId = TaxCircleId.Of(c);
        var inCircle = context.TaxFiles.Where(x => x.CircleId == circleId).Select(x => x.Id);
        demands = demands.Where(d => inCircle.Contains(d.TaxFileId));
      }
      if (!string.IsNullOrWhiteSpace(query.Search))
      {
        var s = query.Search.Trim().ToUpperInvariant();
        var files = context.TaxFiles.Where(x => x.FileNo.ToUpper().Contains(s)).Select(x => x.Id);
        demands = demands.Where(d => d.DemandNo.ToUpper().Contains(s) || d.PayerName.ToUpper().Contains(s) || files.Contains(d.TaxFileId));
      }
      var ids = demands.Select(d => d.Id);
      rows = rows.Where(p => ids.Contains(p.DemandId));
    }
    var total = await rows.LongCountAsync(cancellationToken);
    var sum = await rows.SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;
    var data = await PenaltyRowsAsync(rows, query.Pagination.Pageindex, query.Pagination.PageSize, cancellationToken);
    return Result<GetPenaltiesQueryResult>.Success(new(new PaginatedResult<PenaltyListItemDto>(query.Pagination.Pageindex, query.Pagination.PageSize, total, data), sum));
  }

  private async Task<List<PenaltyListItemDto>> PenaltyRowsAsync(IQueryable<Penalty> rows, int pageIndex, int pageSize, CancellationToken cancellationToken)
  {
    var page = await rows.OrderByDescending(p => p.AppliedOn).ThenBy(p => p.DemandId).Skip(pageIndex * pageSize).Take(pageSize).ToListAsync(cancellationToken);
    var demandIds = page.Select(p => p.DemandId).Distinct().ToList();
    var demands = await context.Demands.AsNoTracking().Where(d => demandIds.Contains(d.Id))
      .Select(d => new { d.Id, d.DemandNo, d.TaxFileId, d.PayerName, d.FiscalYear }).ToDictionaryAsync(d => d.Id, cancellationToken);
    var fileIds = demands.Values.Select(d => d.TaxFileId).Distinct().ToList();
    var files = await context.TaxFiles.AsNoTracking().Where(f => fileIds.Contains(f.Id)).Select(f => new { f.Id, f.FileNo, f.CircleId }).ToDictionaryAsync(f => f.Id, cancellationToken);
    var circles = await context.TaxCircles.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Code, cancellationToken);
    var penaltyIds = page.Select(p => p.Id).ToList();
    var posted = context.Payments.Where(p => p.Status == PaymentStatus.Posted).Select(p => p.Id);
    var paid = await context.PaymentAllocations.AsNoTracking().Where(a => a.PenaltyId != null && penaltyIds.Contains(a.PenaltyId) && posted.Contains(a.PaymentId))
      .GroupBy(a => a.PenaltyId).Select(g => new { g.Key, Paid = g.Sum(a => a.Amount) }).ToListAsync(cancellationToken);
    return page.Select(p =>
    {
      var d = demands[p.DemandId];
      var f = files.GetValueOrDefault(d.TaxFileId);
      return new PenaltyListItemDto(p.Id.Value, p.DemandId.Value, d.DemandNo, d.TaxFileId.Value, f?.FileNo ?? "", d.PayerName, f?.CircleId.Value,
        f is null ? null : circles.GetValueOrDefault(f.CircleId), d.FiscalYear, FiscalYear.Label(d.FiscalYear), p.Kind, p.BaseAmount, p.Pct, p.Amount, p.AppliedOn,
        p.RunId?.Value, p.RuleRef, paid.FirstOrDefault(x => x.Key == p.Id)?.Paid ?? 0m);
    }).ToList();
  }

  private async Task<List<PenaltyRunDto>> RunDtosAsync(IReadOnlyList<PenaltyRun> runs, CancellationToken cancellationToken)
  {
    var circles = await context.TaxCircles.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Code, cancellationToken);
    var rules = await context.PenaltyRules.AsNoTracking().ToDictionaryAsync(r => r.Id, r => new { r.Code, r.Name }, cancellationToken);
    return runs.Select(r => new PenaltyRunDto(r.Id.Value, r.RuleId.Value, rules.GetValueOrDefault(r.RuleId)?.Code, rules.GetValueOrDefault(r.RuleId)?.Name, r.FiscalYear,
      FiscalYear.Label(r.FiscalYear), r.CircleId?.Value, r.CircleId is { } c ? circles.GetValueOrDefault(c) : null, r.RunOn, r.TargetSet, r.RateType, r.Rate,
      r.IncludesCurrent, r.Reference, r.PropertyCount, r.TotalAmount, r.ApprovedBy, r.CreatedAt)).ToList();
  }

  private async Task<SurchargeTerms> TermsAsync(SurchargeRequest r, CancellationToken cancellationToken)
  {
    TaxCircleId? circleId = r.CircleId is { } c ? (await context.LoadCircleAsync(c, cancellationToken)).Id : null;
    return new SurchargeTerms(r.FiscalYear, circleId, r.TargetSet, r.IncludesCurrent, r.RateType, r.Rate, r.Reference);
  }
}
