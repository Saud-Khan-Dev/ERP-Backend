/// taxation.demand_run: the annual computation for a year (all circles, or one). It produces DRAFT demands; approval
/// issues them with their first notices. Computing again replaces the drafts of the earlier computed run of the same scope.
public class DemandRun : Aggregate<DemandRunId>
{
  public int FiscalYear { get; private set; }
  public TaxCircleId? CircleId { get; private set; }
  public RateSetId RateSetId { get; private set; } = default!;
  public DemandRunStatus Status { get; private set; }
  public DateTime BasisSyncedAt { get; private set; }
  public int? PropertyCount { get; private set; }
  public decimal? TotalAmount { get; private set; }
  public int? ExceptionCount { get; private set; }
  public DateTime? ComputedAt { get; private set; }
  public Guid? ComputedBy { get; private set; }
  public DateTime? ApprovedAt { get; private set; }
  public Guid? ApprovedBy { get; private set; }

  /// Concurrency token (Postgres xmin): a run approved twice at once is approved once.
  public uint Version { get; private set; }

  private DemandRun() { }

  public static DemandRun Compute(DemandRunId id, int fiscalYear, TaxCircleId? circleId, RateSet rateSet, Guid? actor, DateTime now)
  {
    if (!rateSet.Covers(global::FiscalYear.Start(fiscalYear)))
      throw new DomainException($"'{rateSet.Name}' is not the approved rate set in force on 1 July {fiscalYear}.");
    return new DemandRun
    {
      Id = id,
      FiscalYear = global::FiscalYear.Valid(fiscalYear),
      CircleId = circleId,
      RateSetId = rateSet.Id,
      Status = DemandRunStatus.Computed,
      BasisSyncedAt = now,
      ComputedAt = now,
      ComputedBy = actor
    };
  }

  public void Record(int propertyCount, decimal totalAmount, int exceptionCount, DateTime basisSyncedAt)
  {
    PropertyCount = propertyCount;
    TotalAmount = totalAmount;
    ExceptionCount = exceptionCount;
    BasisSyncedAt = basisSyncedAt;
  }

  /// Issues the run. A year whose 1 July is still ahead is worked out (to check it) but issued only from that day.
  public void Approve(Guid actor, DateTime now, DateOnly today)
  {
    if (Status != DemandRunStatus.Computed)
      throw new DomainException($"The run is {EnumText.Words(Status)}; only a computed run can be approved.");
    var yearStart = global::FiscalYear.Start(FiscalYear);
    var yearLabel = global::FiscalYear.Label(FiscalYear);
    if (today < yearStart)
      throw new DomainException($"FY {yearLabel} starts on {yearStart:dd MMM yyyy}; its demand is approved from that day.");
    Status = DemandRunStatus.Approved;
    ApprovedBy = actor;
    ApprovedAt = now;
  }

  public void Cancel()
  {
    if (Status != DemandRunStatus.Computed)
      throw new DomainException($"The run is {EnumText.Words(Status)}; only a computed run can be cancelled.");
    Status = DemandRunStatus.Cancelled;
  }
}

/// taxation.penalty_run: one application of the in-year surcharge to a set of properties, approved by the Section.
public class PenaltyRun : Aggregate<PenaltyRunId>
{
  public PenaltyRuleId RuleId { get; private set; } = default!;
  public int FiscalYear { get; private set; }
  public TaxCircleId? CircleId { get; private set; }
  public DateOnly RunOn { get; private set; }
  public TargetSet TargetSet { get; private set; }
  public PenaltyRateType RateType { get; private set; }
  public decimal Rate { get; private set; }
  public bool IncludesCurrent { get; private set; }
  public string? Reference { get; private set; }
  public int? PropertyCount { get; private set; }
  public decimal? TotalAmount { get; private set; }
  public Guid? ApprovedBy { get; private set; }

  /// Concurrency token (Postgres xmin).
  public uint Version { get; private set; }

  private PenaltyRun() { }

  public static PenaltyRun Create(PenaltyRunId id, PenaltyRule rule, SurchargeTerms terms, DateOnly runOn, Guid approvedBy)
  {
    if (rule.Kind != PenaltyKind.IntraYear)
      throw new DomainException($"{rule.Name} is not the in-year surcharge.");
    return new PenaltyRun
    {
      Id = id,
      RuleId = rule.Id,
      FiscalYear = global::FiscalYear.Valid(terms.FiscalYear),
      CircleId = terms.CircleId,
      RunOn = runOn,
      TargetSet = terms.TargetSet,
      RateType = terms.RateType,
      Rate = terms.RateType == PenaltyRateType.Percent ? Guard.Between(terms.Rate, 0.01m, 100m, "Rate %") : Guard.Positive(terms.Rate, "Fixed amount"),
      IncludesCurrent = terms.IncludesCurrent,
      Reference = Guard.Text(terms.Reference, 100, "Reference"),
      ApprovedBy = approvedBy
    };
  }

  public void Record(int count, decimal total)
  {
    PropertyCount = count;
    TotalAmount = total;
  }

  public decimal AmountOn(decimal baseAmount) => RateType == PenaltyRateType.Percent ? Rupees.Round(baseAmount * Rate / 100m) : Rupees.Round(Rate);
}

/// What the Section decides for an in-year surcharge run.
public sealed record SurchargeTerms(int FiscalYear, TaxCircleId? CircleId, TargetSet TargetSet, bool IncludesCurrent, PenaltyRateType RateType, decimal Rate, string? Reference);

/// One demand's place in a surcharge run: the base, the amount, and whether it can be surcharged today (SurchargedOn: the
/// day an earlier run of the same rule surcharged it, which makes it not eligible).
public sealed record SurchargeCandidate(Demand Demand, decimal Base, decimal Amount, bool Eligible, string? Reason, decimal ArrearsOwed, DateOnly? SurchargedOn);

public static class SurchargeRules
{
  /// The unpaid demands of the year a run would surcharge: defaulters only (still owing arrears of earlier years) or all
  /// unpaid; the base is the balance, or only what the arrears still owe when the current year is left out. A demand is
  /// eligible once the rule's period has run, while no litigation hold is open, and only once a year: a demand an earlier
  /// run of the same rule surcharged is not surcharged again.
  public static SurchargeCandidate? Candidate(Demand demand, TaxFile file, PenaltyRule rule, SurchargeTerms terms, TaxSettings settings, DateOnly today)
  {
    if (demand.FiscalYear != terms.FiscalYear || demand.Status is not (DemandStatus.Issued or DemandStatus.PartPaid) || demand.Balance <= 0)
      return null;
    if (terms.CircleId is not null && file.CircleId != terms.CircleId)
      return null;
    var arrearsOwed = demand.ArrearsOwed;
    if (terms.TargetSet == TargetSet.DefaultersOnly && arrearsOwed <= 0)
      return null;

    var baseAmount = terms.IncludesCurrent ? demand.Balance : Math.Min(demand.Balance, arrearsOwed);
    var amount = terms.RateType == PenaltyRateType.Percent ? Rupees.Round(baseAmount * terms.Rate / 100m) : Rupees.Round(terms.Rate);
    var from = rule.EligibleFrom(demand.FiscalYear, demand.IssuedOn, demand.DueDate ?? settings.DueDate(demand.FiscalYear));
    var earlier = demand.Penalties.Where(p => p.Kind == PenaltyKind.IntraYear && p.RuleRef == rule.Code).OrderByDescending(p => p.AppliedOn).FirstOrDefault();
    var held = file.HoldOn(today);
    var reason = earlier is not null ? $"surcharged on {earlier.AppliedOn:dd MMM yyyy}"
      : held is not null ? "litigation hold"
      : from > today ? $"period runs until {from:dd MMM yyyy}"
      : baseAmount <= 0 ? "no arrears" : null;
    return new SurchargeCandidate(demand, baseAmount, amount, reason is null && amount > 0, reason, arrearsOwed, earlier?.AppliedOn);
  }
}
