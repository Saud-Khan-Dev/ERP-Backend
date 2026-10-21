/// taxation.tax_file_basis: the Property facts the formulas use, as copied on one sync. Each change is a new row, so a
/// demand points at the exact basis it was computed on.
public class TaxFileBasis : Entity<TaxFileBasisId>
{
  public const decimal SqftPerSqyd = 9m;
  public const decimal SqftPerKanal = 5445m;

  public TaxFileId TaxFileId { get; private set; } = default!;
  public decimal? LandAreaSqyd { get; private set; }
  public decimal? LandAreaEntered { get; private set; }
  public string? LandAreaUnitCode { get; private set; }
  public decimal? LandAreaKanal { get; private set; }
  public decimal? CoveredAreaSqft { get; private set; }
  public CoveredAreaSource? CoveredAreaSource { get; private set; }
  public decimal? RegularizedAreaSqyd { get; private set; }
  public Guid? SourceMeasurementId { get; private set; }
  public Guid? SourceBuildingPlanId { get; private set; }
  public DateOnly? BpaDate { get; private set; }
  public Guid? CompletionPlanId { get; private set; }
  public DateOnly? CompletionDate { get; private set; }
  public Guid? PropertyTypeId { get; private set; }
  public string? PropertyStatusCode { get; private set; }
  public string? ClassificationCode { get; private set; }
  public DateTime SyncedAt { get; private set; }
  public string? SourceVersion { get; private set; }
  public bool IsCurrent { get; private set; }

  private TaxFileBasis() { }

  /// A = the land the property tax is charged on in sq yd: the measured area plus any regularized area.
  public decimal? TaxableLandSqyd => LandAreaSqyd is null ? null : LandAreaSqyd + (RegularizedAreaSqyd ?? 0m);

  /// The land in kanal, regularized area included (conservancy and water count kanal).
  public decimal? TaxableKanal => TaxableLandSqyd is null ? null : Math.Round(TaxableLandSqyd.Value * SqftPerSqyd / SqftPerKanal, 4);

  internal static TaxFileBasis From(TaxFileBasisId id, TaxFileId fileId, PropertySnapshot s, DateTime now) => new()
  {
    Id = id,
    TaxFileId = fileId,
    LandAreaSqyd = s.LandAreaSqft is { } land ? Math.Round(land / SqftPerSqyd, 4) : null,
    LandAreaEntered = s.LandAreaEntered,
    LandAreaUnitCode = s.LandAreaUnitCode,
    LandAreaKanal = s.LandAreaSqft is { } k ? Math.Round(k / SqftPerKanal, 4) : null,
    CoveredAreaSqft = s.CoveredAreaSqft is { } covered ? Math.Round(covered, 4) : null,
    CoveredAreaSource = s.CoveredAreaSource,
    RegularizedAreaSqyd = s.RegularizedAreaSqft is { } reg && reg > 0 ? Math.Round(reg / SqftPerSqyd, 4) : null,
    SourceMeasurementId = s.SourceMeasurementId,
    SourceBuildingPlanId = s.SourceBuildingPlanId,
    BpaDate = s.BpaDate,
    CompletionPlanId = s.CompletionPlanId,
    CompletionDate = s.CompletionDate,
    PropertyTypeId = s.PropertyTypeId,
    PropertyStatusCode = s.PropertyStatusCode,
    ClassificationCode = s.ClassificationCode,
    SyncedAt = now,
    SourceVersion = s.SourceVersion,
    IsCurrent = true
  };

  internal bool SameFacts(TaxFileBasis other) =>
      LandAreaSqyd == other.LandAreaSqyd && LandAreaEntered == other.LandAreaEntered && LandAreaUnitCode == other.LandAreaUnitCode
      && CoveredAreaSqft == other.CoveredAreaSqft && CoveredAreaSource == other.CoveredAreaSource && RegularizedAreaSqyd == other.RegularizedAreaSqyd
      && SourceMeasurementId == other.SourceMeasurementId && SourceBuildingPlanId == other.SourceBuildingPlanId && BpaDate == other.BpaDate
      && CompletionPlanId == other.CompletionPlanId && CompletionDate == other.CompletionDate && PropertyTypeId == other.PropertyTypeId
      && PropertyStatusCode == other.PropertyStatusCode && ClassificationCode == other.ClassificationCode;

  internal void Supersede() => IsCurrent = false;

  internal void MarkSynced(DateTime now) => SyncedAt = now;
}

/// taxation.tax_file_payer: who pays, copied from Property (an owner's share, the lessee, the tenant or the allottee). A
/// completed transfer closes these rows and opens new ones on the next sync.
public class TaxFilePayer : Entity<TaxFilePayerId>
{
  public TaxFileId TaxFileId { get; private set; } = default!;
  public Guid OwnerId { get; private set; }
  public string OwnerCode { get; private set; } = default!;
  public PayerRole PayerRole { get; private set; }
  public PayerSource SourceType { get; private set; }
  public Guid SourceId { get; private set; }
  public decimal? SharePct { get; private set; }
  public bool IsAddressee { get; private set; }
  public string Name { get; private set; } = default!;
  public string? FatherHusbandName { get; private set; }
  public string? CnicNtn { get; private set; }
  public string? Mobile { get; private set; }
  public string? PostalAddress { get; private set; }
  public DateOnly EffectiveFrom { get; private set; }
  public DateOnly? EffectiveTo { get; private set; }
  public DateTime SyncedAt { get; private set; }

  private TaxFilePayer() { }

  /// Who a demand is addressed to by default: the tenant of a rental, else the lessee, else the owner with the largest
  /// share, else the allottee.
  internal int Priority => PayerRole switch { PayerRole.Tenant => 0, PayerRole.Lessee => 1, PayerRole.Owner => 2, _ => 3 };

  public bool Covers(DateOnly date) => EffectiveFrom <= date && (EffectiveTo is null || EffectiveTo >= date);

  internal static TaxFilePayer Create(TaxFilePayerId id, TaxFileId fileId, PayerSnapshot s, DateOnly from, DateTime now)
  {
    var payer = new TaxFilePayer { Id = id, TaxFileId = fileId, OwnerId = s.OwnerId, SourceType = s.SourceType, SourceId = s.SourceId, EffectiveFrom = from };
    payer.Refresh(s, now);
    return payer;
  }

  internal void Refresh(PayerSnapshot s, DateTime now)
  {
    OwnerCode = Guard.RequiredText(s.OwnerCode, 20, "Owner code");
    PayerRole = s.Role;
    SharePct = s.SharePct is { } share ? Math.Round(share, 4) : null;
    Name = Guard.RequiredText(s.Name, 200, "Payer name");
    FatherHusbandName = Trim(s.FatherHusbandName, 200);
    CnicNtn = Trim(s.CnicNtn, 20);
    Mobile = Trim(s.Mobile, 30);
    PostalAddress = Trim(s.PostalAddress, 400);
    SyncedAt = now;
  }

  internal void Close(DateOnly lastDay) => EffectiveTo = lastDay;

  internal void SetAddressee(bool value) => IsAddressee = value;

  private static string? Trim(string? value, int max) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().Length <= max ? value.Trim() : value.Trim()[..max];
}

/// taxation.tax_file_head_value: what an admin-defined head needs per property: the date a facility was taken (water
/// connection) or a quantity (sq ft of signboard).
public class TaxFileHeadValue : Entity<TaxFileHeadValueId>
{
  public TaxFileId TaxFileId { get; private set; } = default!;
  public TaxHeadId HeadId { get; private set; } = default!;
  public DateOnly? FacilityFrom { get; private set; }
  public decimal? Quantity { get; private set; }

  private TaxFileHeadValue() { }

  internal static TaxFileHeadValue Create(TaxFileHeadValueId id, TaxFileId fileId, TaxHeadId headId, DateOnly? facilityFrom, decimal? quantity) =>
      new() { Id = id, TaxFileId = fileId, HeadId = headId, FacilityFrom = facilityFrom, Quantity = quantity };

  internal void Set(DateOnly? facilityFrom, decimal? quantity)
  {
    FacilityFrom = facilityFrom;
    Quantity = quantity;
  }
}

/// taxation.tax_file_override: a per-property, per-head change to the formula: SPECIAL (a fixed yearly amount, or a
/// special policy) or MINIMUM (the computed amount may not fall below it).
public class TaxFileOverride : Entity<TaxFileOverrideId>
{
  public TaxFileId TaxFileId { get; private set; } = default!;
  public TaxHeadId HeadId { get; private set; } = default!;
  public OverrideMode Mode { get; private set; }
  public SpecialPolicyId? PolicyId { get; private set; }
  public decimal? Amount { get; private set; }
  public bool FollowsIncrease { get; private set; }
  public string Reason { get; private set; } = default!;
  public string? Reference { get; private set; }
  public DateOnly EffectiveFrom { get; private set; }
  public DateOnly? EffectiveTo { get; private set; }
  public Guid? ApprovedBy { get; private set; }
  public DateTime? ApprovedAt { get; private set; }

  private TaxFileOverride() { }

  public bool Covers(DateOnly date) => EffectiveFrom <= date && (EffectiveTo is null || EffectiveTo >= date);

  internal static TaxFileOverride Create(TaxFileOverrideId id, TaxFileId fileId, TaxHeadId headId, OverrideMode mode, SpecialPolicyId? policyId, decimal? amount,
      bool followsIncrease, string reason, string? reference, DateOnly from, Guid actor, DateTime now) => new()
  {
    Id = id,
    TaxFileId = fileId,
    HeadId = headId,
    Mode = mode,
    PolicyId = policyId,
    Amount = amount is { } a ? Guard.Money(a, "Amount") : null,
    FollowsIncrease = followsIncrease,
    Reason = Guard.RequiredText(reason, 300, "Reason"),
    Reference = Guard.Text(reference, 100, "Reference"),
    EffectiveFrom = from,
    ApprovedBy = actor,
    ApprovedAt = now
  };

  internal void End(DateOnly lastDay)
  {
    if (EffectiveTo is not null && EffectiveTo <= lastDay)
      throw new DomainException($"The override already ended on {EffectiveTo:yyyy-MM-dd}.");
    if (lastDay < EffectiveFrom)
      throw new DomainException($"The override starts on {EffectiveFrom:yyyy-MM-dd}; it cannot end before that.");
    EffectiveTo = lastDay;
  }

  /// The override that ended because of one taken back runs on again.
  internal void Reopen(DateOnly endedOn)
  {
    if (EffectiveTo == endedOn)
      EffectiveTo = null;
  }
}

/// taxation.litigation_hold: while open no tax or fee is levied on the property, and the held days do not count toward
/// the completion-certificate grace period.
public class LitigationHold : Entity<LitigationHoldId>
{
  public TaxFileId TaxFileId { get; private set; } = default!;
  public Guid? SourceLitigationId { get; private set; }
  public string? Reference { get; private set; }
  public string Reason { get; private set; } = default!;
  public DateOnly StartedOn { get; private set; }
  public DateOnly? EndedOn { get; private set; }
  public bool SuspendsDemand { get; private set; }
  public bool ExcludesCompletionClock { get; private set; }

  private LitigationHold() { }

  public bool Covers(DateOnly date) => StartedOn <= date && (EndedOn is null || EndedOn >= date);

  internal static LitigationHold Create(LitigationHoldId id, TaxFileId fileId, string reason, string? reference, Guid? sourceLitigationId, DateOnly startedOn,
      bool suspendsDemand, bool excludesCompletionClock) => new()
  {
    Id = id,
    TaxFileId = fileId,
    Reason = Guard.RequiredText(reason, 300, "Reason"),
    Reference = Guard.Text(reference, 100, "Reference"),
    SourceLitigationId = sourceLitigationId,
    StartedOn = startedOn,
    SuspendsDemand = suspendsDemand,
    ExcludesCompletionClock = excludesCompletionClock
  };

  internal void End(DateOnly endedOn)
  {
    if (EndedOn is not null)
      throw new DomainException($"The hold already ended on {EndedOn:yyyy-MM-dd}.");
    if (endedOn < StartedOn)
      throw new DomainException("A hold cannot end before it started.");
    EndedOn = endedOn;
  }
}

/// taxation.agreement_schedule: a rent or lease agreement copied from Property, with what Taxation adds: how often the
/// amount steps up and by how much (a rental: its annual increase, every year unless the Section says otherwise; a
/// lease: 10 % every 5 years), and mid-term revisions of the step (agreement_step_revision).
public class AgreementSchedule : Entity<AgreementScheduleId>
{
  private readonly List<AgreementStepRevision> _revisions = new();

  public TaxFileId TaxFileId { get; private set; } = default!;
  public AgreementKind Kind { get; private set; }
  public Guid SourceId { get; private set; }
  public string SourceNo { get; private set; } = default!;
  public decimal BaseAmount { get; private set; }
  public AmountFrequency Frequency { get; private set; }
  public DateOnly StartDate { get; private set; }
  public DateOnly? EndDate { get; private set; }
  public int? TermYears { get; private set; }
  public decimal? StepPct { get; private set; }
  public int? StepIntervalYears { get; private set; }
  public bool IsDirect99Year { get; private set; }
  public DateOnly? NextRenewalDate { get; private set; }
  public AgreementStatus Status { get; private set; }

  public IReadOnlyList<AgreementStepRevision> Revisions => _revisions.AsReadOnly();

  private AgreementSchedule() { }

  /// The amount a year: monthly x 12, quarterly x 4; a one-time amount is not billed every year.
  public decimal AnnualBase => Frequency switch
  {
    AmountFrequency.Monthly => BaseAmount * 12,
    AmountFrequency.Quarterly => BaseAmount * 4,
    AmountFrequency.Annual => BaseAmount,
    _ => 0m
  };

  internal static AgreementSchedule Create(AgreementScheduleId id, TaxFileId fileId, AgreementSnapshot s) => new()
  {
    Id = id,
    TaxFileId = fileId,
    Kind = s.Kind,
    SourceId = s.SourceId,
    SourceNo = Guard.RequiredText(s.SourceNo, 50, "Agreement no."),
    BaseAmount = Guard.Money(s.BaseAmount, "Agreement amount"),
    Frequency = s.Frequency,
    StartDate = s.StartDate,
    EndDate = s.EndDate,
    TermYears = s.TermYears,
    StepPct = s.StepPct ?? (s.Kind == AgreementKind.Lease ? 10m : 0m),
    StepIntervalYears = s.Kind == AgreementKind.Lease ? 5 : 1,
    Status = s.IsActive ? AgreementStatus.Active : AgreementStatus.Ended
  };

  internal void Refresh(AgreementSnapshot s)
  {
    SourceNo = Guard.RequiredText(s.SourceNo, 50, "Agreement no.");
    BaseAmount = Guard.Money(s.BaseAmount, "Agreement amount");
    Frequency = s.Frequency;
    StartDate = s.StartDate;
    EndDate = s.EndDate;
    TermYears = s.TermYears;
    Status = s.IsActive ? AgreementStatus.Active : AgreementStatus.Ended;
  }

  internal void End() => Status = AgreementStatus.Ended;

  /// The Section's terms: the step and its interval, and the open points (direct 99-year lease, next renewal).
  public void SetTerms(decimal stepPct, int stepIntervalYears, bool isDirect99Year, DateOnly? nextRenewalDate)
  {
    StepPct = Guard.Between(stepPct, 0m, 100m, "Step %");
    StepIntervalYears = Guard.Between(stepIntervalYears, 1, 99, "Step every (years)");
    IsDirect99Year = isDirect99Year;
    NextRenewalDate = nextRenewalDate;
  }

  /// A mid-term change of the step from a date; past years keep the old step.
  public AgreementStepRevision Revise(DateOnly effectiveFrom, decimal stepPct, int stepIntervalYears, string? boardReference)
  {
    if (effectiveFrom <= StartDate)
      throw new DomainException("A revision takes effect after the agreement started; change the terms instead.");
    if (_revisions.Any(r => r.EffectiveFrom >= effectiveFrom))
      throw new DomainException("The agreement already has a revision from that date or later.");
    var revision = AgreementStepRevision.Create(AgreementStepRevisionId.New(), Id, effectiveFrom, stepPct, stepIntervalYears, boardReference);
    _revisions.Add(revision);
    return revision;
  }

  /// Whether the agreement runs during any part of a fiscal year.
  public bool RunsDuring(int fiscalYear) => StartDate <= FiscalYear.End(fiscalYear) && (EndDate is null || EndDate >= FiscalYear.Start(fiscalYear));

  /// The rent of a year: the annual base stepped up on each anniversary that falls due by 1 July of the year, by the
  /// step (and interval) in force on that anniversary.
  public (decimal Amount, string Explanation)? AmountFor(int fiscalYear)
  {
    if (!RunsDuring(fiscalYear) || AnnualBase <= 0)
      return null;

    var yearStart = FiscalYear.Start(fiscalYear);
    var amount = AnnualBase;
    var steps = 0;
    for (var k = 1; k < 200; k++)
    {
      var anniversary = Anniversary(k);
      if (anniversary > yearStart)
        break;
      var (pct, every) = StepOn(anniversary);
      if (every > 0 && k % every == 0)
      {
        amount *= 1 + pct / 100m;
        steps++;
      }
    }

    var current = _revisions.Where(r => r.EffectiveFrom <= yearStart).OrderByDescending(r => r.EffectiveFrom).FirstOrDefault();
    var (curPct, curEvery) = StepOn(yearStart);
    var text = $"agreement {SourceNo}: base {Rupees.Text(AnnualBase)} a year from {StartDate:yyyy-MM-dd}, +{Rupees.Number(curPct)} % every {curEvery} yr"
      + (current is null ? "" : $" (revised {current.EffectiveFrom:yyyy-MM-dd}{(current.BoardReference is null ? "" : ", " + current.BoardReference)})")
      + $"; {steps} step(s) so far";
    return (Rupees.Round(amount), text);
  }

  private DateOnly Anniversary(int years)
  {
    var year = StartDate.Year + years;
    return new DateOnly(year, StartDate.Month, Math.Min(StartDate.Day, DateTime.DaysInMonth(year, StartDate.Month)));
  }

  private (decimal Pct, int Every) StepOn(DateOnly date)
  {
    var revision = _revisions.Where(r => r.EffectiveFrom <= date).OrderByDescending(r => r.EffectiveFrom).FirstOrDefault();
    return revision is null ? (StepPct ?? 0m, StepIntervalYears ?? 1) : (revision.StepPct, revision.StepIntervalYears);
  }
}

/// taxation.agreement_step_revision: a mid-term change of a rent or lease increase (15 % revised to 7 % after two years).
public class AgreementStepRevision : Entity<AgreementStepRevisionId>
{
  public AgreementScheduleId ScheduleId { get; private set; } = default!;
  public DateOnly EffectiveFrom { get; private set; }
  public decimal StepPct { get; private set; }
  public int StepIntervalYears { get; private set; }
  public string? BoardReference { get; private set; }

  private AgreementStepRevision() { }

  internal static AgreementStepRevision Create(AgreementStepRevisionId id, AgreementScheduleId scheduleId, DateOnly from, decimal pct, int every, string? boardReference) => new()
  {
    Id = id,
    ScheduleId = scheduleId,
    EffectiveFrom = from,
    StepPct = Guard.Between(pct, 0m, 100m, "Step %"),
    StepIntervalYears = Guard.Between(every, 1, 99, "Step every (years)"),
    BoardReference = Guard.Text(boardReference, 100, "Board reference")
  };
}
