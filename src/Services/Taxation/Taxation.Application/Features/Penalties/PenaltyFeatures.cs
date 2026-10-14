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
      ?? throw new DomainException("No in-year surcharge rule is in force. Set it in Settings → Penalty rules.");
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
  IClock clock) :
  IQueryHandler<PreviewSurchargeQuery, Result<SurchargePreviewDto>>,
  ICommandHandler<ApplySurchargeCommand, Result<ApplySurchargeResult>>,
  ICommandHandler<ApplyYearEndCommand, Result<ApplyYearEndResult>>,
  IQueryHandler<GetPenaltyRunsQuery, Result<GetPenaltyRunsQueryResult>>
{
  public async Task<Result<SurchargePreviewDto>> Handle(PreviewSurchargeQuery query, CancellationToken cancellationToken)
  {
    var (_, rows) = await penalties.CandidatesAsync(await TermsAsync(query.Terms, cancellationToken), cancellationToken);
    var list = rows.Select(r => new SurchargeCandidateDto(r.Candidate.Demand.Id.Value, r.Candidate.Demand.DemandNo, r.File.Id.Value, r.File.FileNo,
      r.Candidate.Demand.PayerName, r.Candidate.Demand.Balance, r.Candidate.Demand.ArrearsBf, r.Candidate.Base, r.Candidate.Amount, r.Candidate.Eligible,
      r.Candidate.Reason)).ToList();
    return Result<SurchargePreviewDto>.Success(new(list.Count(c => c.Eligible), list.Where(c => c.Eligible).Sum(c => c.Amount), list));
  }

  public async Task<Result<ApplySurchargeResult>> Handle(ApplySurchargeCommand command, CancellationToken cancellationToken)
  {
    var terms = await TermsAsync(command.Terms, cancellationToken);
    var (rule, rows) = await penalties.CandidatesAsync(terms, cancellationToken);
    var eligible = rows.Where(r => r.Candidate.Eligible).ToList();
    if (eligible.Count == 0)
      return Result<ApplySurchargeResult>.Failure($"No property is eligible for this surcharge on {clock.Today:yyyy-MM-dd}.");

    var actor = Guard.Actor(currentUser.UserId, "approve a surcharge run");
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

    await context.SaveChangesAsync(cancellationToken);
    return Result<ApplySurchargeResult>.Success(new(run.Id.Value, eligible.Count, run.TotalAmount ?? 0, prepared));
  }

  public async Task<Result<ApplyYearEndResult>> Handle(ApplyYearEndCommand command, CancellationToken cancellationToken)
  {
    var count = await penalties.ApplyYearEndAsync(cancellationToken);
    await context.SaveChangesAsync(cancellationToken);
    return Result<ApplyYearEndResult>.Success(new(count));
  }

  public async Task<Result<GetPenaltyRunsQueryResult>> Handle(GetPenaltyRunsQuery query, CancellationToken cancellationToken)
  {
    var runs = await context.PenaltyRuns.AsNoTracking().Where(r => query.FiscalYear == null || r.FiscalYear == query.FiscalYear)
      .OrderByDescending(r => r.RunOn).ThenByDescending(r => r.CreatedAt).ToListAsync(cancellationToken);
    var circles = await context.TaxCircles.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Code, cancellationToken);
    return Result<GetPenaltyRunsQueryResult>.Success(new(runs.Select(r => new PenaltyRunDto(r.Id.Value, r.RuleId.Value, r.FiscalYear, FiscalYear.Label(r.FiscalYear),
      r.CircleId?.Value, r.CircleId is { } c ? circles.GetValueOrDefault(c) : null, r.RunOn, r.TargetSet, r.RateType, r.Rate, r.IncludesCurrent, r.Reference,
      r.PropertyCount, r.TotalAmount, r.ApprovedBy, r.CreatedAt)).ToList()));
  }

  private async Task<SurchargeTerms> TermsAsync(SurchargeRequest r, CancellationToken cancellationToken)
  {
    TaxCircleId? circleId = r.CircleId is { } c ? (await context.LoadCircleAsync(c, cancellationToken)).Id : null;
    return new SurchargeTerms(r.FiscalYear, circleId, r.TargetSet, r.IncludesCurrent, r.RateType, r.Rate, r.Reference);
  }
}
