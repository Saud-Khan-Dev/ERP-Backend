/// taxation.tax_file: one taxable property. The facts the formulas use are copied from Property (tax_file_basis, a new
/// row whenever they change) together with who pays (tax_file_payer); what Property does not hold (rooms, registration
/// date, plot no., facility dates, quantities, overrides, holds, rent schedules) is kept here. The 1Link / 1Bill
/// reference (psid) is issued once and never changes; the amount behind it is re-synced.
public class TaxFile : Aggregate<TaxFileId>
{
  private readonly List<TaxFileBasis> _bases = new();
  private readonly List<TaxFilePayer> _payers = new();
  private readonly List<TaxFileHeadValue> _headValues = new();
  private readonly List<TaxFileOverride> _overrides = new();
  private readonly List<LitigationHold> _holds = new();
  private readonly List<AgreementSchedule> _agreements = new();

  public string FileNo { get; private set; } = default!;
  public Guid PropertyId { get; private set; }
  public string PropertyCode { get; private set; } = default!;
  public TaxCircleId CircleId { get; private set; } = default!;
  public Guid TownId { get; private set; }
  public TaxCategory TaxCategory { get; private set; }
  public HoldingKind HoldingKind { get; private set; }
  public int? Rooms { get; private set; }
  public string? Psid { get; private set; }
  public DateOnly? RegisteredOn { get; private set; }
  public DateOnly? WaterFacilityFrom { get; private set; }
  public UpliftRateBasis? UpliftRateBasis { get; private set; }
  public decimal? UpliftPct { get; private set; }
  public string? OldReferenceNo { get; private set; }
  public string? PlotNo { get; private set; }
  public TaxStatus TaxStatus { get; private set; }
  public string? ExemptionReason { get; private set; }
  public DateOnly OpenedOn { get; private set; }
  public DateOnly? ClosedOn { get; private set; }
  public string? Remarks { get; private set; }

  /// Concurrency token (Postgres xmin). A change to anything on the file touches the file row, so two people changing
  /// the same file at once (two holds, two overrides) cannot both win.
  public uint Version { get; private set; }

  public IReadOnlyList<TaxFileBasis> Bases => _bases.AsReadOnly();
  public IReadOnlyList<TaxFilePayer> Payers => _payers.AsReadOnly();
  public IReadOnlyList<TaxFileHeadValue> HeadValues => _headValues.AsReadOnly();
  public IReadOnlyList<TaxFileOverride> Overrides => _overrides.AsReadOnly();
  public IReadOnlyList<LitigationHold> Holds => _holds.AsReadOnly();
  public IReadOnlyList<AgreementSchedule> Agreements => _agreements.AsReadOnly();

  private TaxFile() { }

  public TaxFileBasis? CurrentBasis => _bases.Where(b => b.IsCurrent).OrderByDescending(b => b.SyncedAt).FirstOrDefault();

  public IReadOnlyList<TaxFilePayer> CurrentPayers(DateOnly asOf) => _payers.Where(p => p.Covers(asOf)).ToList();

  /// The payer the demand and its notices are addressed to.
  public TaxFilePayer? Addressee(DateOnly asOf) => CurrentPayers(asOf).FirstOrDefault(p => p.IsAddressee) ?? CurrentPayers(asOf).FirstOrDefault();

  public bool IsAssessable => TaxStatus == TaxStatus.Active && TaxCategory != TaxCategory.Exempt;

  /// Marks the file changed when only something under it changed (an override, a hold), so its concurrency token is checked.
  private void Touch() => UpdatedAt = DateTime.UtcNow;

  /// Whether the uplift settings of the file would change (they change the tax like an override, so need approval).
  public bool UpliftDiffers(TaxFileDetails details) => details.UpliftRateBasis != UpliftRateBasis || details.UpliftPct != UpliftPct;

  public static TaxFile Open(TaxFileId id, string fileNo, string psid, PropertySnapshot snapshot, TaxCircleId circleId, TaxCategory category, TaxFileDetails details, DateOnly openedOn, DateTime now)
  {
    if (snapshot.PropertyId == Guid.Empty)
      throw new DomainException("Choose the property.");
    if (!snapshot.IsActive)
      throw new DomainException($"{snapshot.PropertyCode} is deactivated in Property; a tax file cannot be opened for it.");

    var file = new TaxFile
    {
      Id = id,
      FileNo = Guard.RequiredText(fileNo, 40, "File no."),
      PropertyId = snapshot.PropertyId,
      PropertyCode = Guard.RequiredText(snapshot.PropertyCode, 40, "Property code"),
      CircleId = circleId,
      TownId = snapshot.TownId ?? throw new DomainException($"{snapshot.PropertyCode} has no town in Property."),
      TaxCategory = category,
      HoldingKind = snapshot.HoldingKind,
      Psid = Guard.RequiredText(psid, 30, "Reference"),
      TaxStatus = category == TaxCategory.Exempt ? TaxStatus.Exempt : TaxStatus.Active,
      OpenedOn = openedOn
    };
    file.Update(details);
    if (file.TaxStatus == TaxStatus.Exempt)
      file.ExemptionReason = Guard.Text(details.ExemptionReason, 300, "Exemption reason") ?? "Exempt property type";

    file._bases.Add(TaxFileBasis.From(TaxFileBasisId.New(), id, snapshot, now));
    file.SyncPayers(snapshot.Payers, openedOn, now);
    file.SyncAgreements(snapshot.Agreements);
    return file;
  }

  public void Update(TaxFileDetails details)
  {
    Rooms = details.Rooms is { } rooms ? Guard.Between(rooms, 0, 5000, "Rooms") : null;
    RegisteredOn = details.RegisteredOn;
    UpliftRateBasis = details.UpliftRateBasis;
    UpliftPct = details.UpliftPct is { } pct ? Guard.Between(pct, 0m, 1000m, "Uplift %") : null;
    OldReferenceNo = Guard.Text(details.OldReferenceNo, 50, "Old reference no.");
    PlotNo = Guard.Text(details.PlotNo, 30, "Plot no.");
    Remarks = Guard.Text(details.Remarks, 4000, "Remarks");
  }

  /// A plot no. typed on a notice is kept for the file when it has none.
  public void FillPlotNo(string? plotNo)
  {
    if (PlotNo is null && Guard.Text(plotNo, 30, "Plot no.") is { } value)
      PlotNo = value;
  }

  /// Moves the file to another category (from the property type, or by the Section with a reason).
  public void ChangeCategory(TaxCategory category, string? reason)
  {
    if (category == TaxCategory)
      return;
    if (category == TaxCategory.Exempt)
    {
      TaxStatus = TaxStatus.Exempt;
      ExemptionReason = Guard.RequiredText(reason, 300, "Exemption reason");
    }
    else if (TaxStatus == TaxStatus.Exempt)
    {
      TaxStatus = TaxStatus.Active;
      ExemptionReason = null;
    }
    TaxCategory = category;
  }

  public void ChangeStatus(TaxStatus status, string? reason, DateOnly today)
  {
    switch (status)
    {
      case TaxStatus.Active:
        if (TaxCategory == TaxCategory.Exempt)
          throw new DomainException("An exempt property is active again only by changing its category.");
        TaxStatus = TaxStatus.Active;
        ExemptionReason = null;
        ClosedOn = null;
        break;
      case TaxStatus.Exempt:
        TaxStatus = TaxStatus.Exempt;
        ExemptionReason = Guard.RequiredText(reason, 300, "Exemption reason");
        break;
      case TaxStatus.Closed:
        if (TaxStatus == TaxStatus.Closed)
          throw new DomainException("The tax file is already closed.");
        TaxStatus = TaxStatus.Closed;
        ClosedOn = today;
        Remarks = Guard.Text(reason, 4000, "Reason") ?? Remarks;
        break;
      default:
        throw new DomainException("A file is sealed through the sealing order, not by a status change.");
    }
  }

  public void MoveToCircle(TaxCircleId circleId, Guid townId)
  {
    CircleId = circleId;
    TownId = townId;
  }

  // ---- facts from Property ----

  /// Applies a fresh copy of the Property facts: a new basis row when anything the formulas use changed, payers closed
  /// and opened, new agreements added. Returns whether the basis changed.
  public bool Sync(PropertySnapshot snapshot, DateOnly today, DateTime now)
  {
    if (snapshot.PropertyId != PropertyId)
      throw new DomainException("The Property data is for another property.");
    PropertyCode = snapshot.PropertyCode;
    HoldingKind = snapshot.HoldingKind;

    var changed = false;
    var current = CurrentBasis;
    var next = TaxFileBasis.From(TaxFileBasisId.New(), Id, snapshot, now);
    if (current is null || !current.SameFacts(next))
    {
      current?.Supersede();
      _bases.Add(next);
      changed = true;
    }
    else
    {
      current.MarkSynced(now);
    }

    SyncPayers(snapshot.Payers, today, now);
    SyncAgreements(snapshot.Agreements);
    return changed;
  }

  private void SyncPayers(IReadOnlyList<PayerSnapshot> payers, DateOnly today, DateTime now)
  {
    var open = _payers.Where(p => p.EffectiveTo is null).ToList();
    foreach (var payer in open.Where(p => !payers.Any(n => n.Matches(p))))
      payer.Close(today.AddDays(-1) < payer.EffectiveFrom ? payer.EffectiveFrom : today.AddDays(-1));

    foreach (var incoming in payers)
    {
      var existing = open.FirstOrDefault(incoming.Matches);
      if (existing is not null)
        existing.Refresh(incoming, now);
      else
        _payers.Add(TaxFilePayer.Create(TaxFilePayerId.New(), Id, incoming, incoming.EffectiveFrom ?? today, now));
    }

    // keep exactly one addressee among the current payers: the one chosen, else the first by priority
    var current = _payers.Where(p => p.EffectiveTo is null).ToList();
    if (current.Count > 0 && current.Count(p => p.IsAddressee) != 1)
    {
      foreach (var p in current)
        p.SetAddressee(false);
      current.OrderBy(p => p.Priority).ThenByDescending(p => p.SharePct ?? 0).ThenBy(p => p.Name).First().SetAddressee(true);
    }
  }

  public void ChooseAddressee(TaxFilePayerId payerId)
  {
    var payer = _payers.FirstOrDefault(p => p.Id == payerId && p.EffectiveTo is null)
      ?? throw new DomainException("Choose one of the current payers of the file.");
    foreach (var p in _payers.Where(p => p.EffectiveTo is null))
      p.SetAddressee(p == payer);
    Touch();
  }

  private void SyncAgreements(IReadOnlyList<AgreementSnapshot> agreements)
  {
    foreach (var incoming in agreements)
    {
      var existing = _agreements.FirstOrDefault(a => a.SourceId == incoming.SourceId);
      if (existing is null)
        _agreements.Add(AgreementSchedule.Create(AgreementScheduleId.New(), Id, incoming));
      else
        existing.Refresh(incoming);
    }
    foreach (var gone in _agreements.Where(a => a.Status == AgreementStatus.Active && !agreements.Any(n => n.SourceId == a.SourceId)))
      gone.End();
  }

  // ---- per-head values ----

  public TaxFileHeadValue? HeadValue(TaxHeadId headId) => _headValues.FirstOrDefault(v => v.HeadId == headId);

  /// The facility date of a FACILITY head (water from the water connection) or the quantity of a CUSTOM head.
  public void SetHeadValue(TaxHead head, DateOnly? facilityFrom, decimal? quantity)
  {
    if (head.HeadKind != HeadKind.Regular || head.IsBuiltin)
      throw new DomainException($"{head.Name} takes no per-property value.");
    if (quantity is < 0)
      throw new DomainException("The quantity cannot be negative.");

    var value = HeadValue(head.Id);
    if (facilityFrom is null && quantity is null)
    {
      if (value is not null)
        _headValues.Remove(value);
    }
    else if (value is null)
    {
      _headValues.Add(TaxFileHeadValue.Create(TaxFileHeadValueId.New(), Id, head.Id, facilityFrom, quantity));
    }
    else
    {
      value.Set(facilityFrom, quantity);
    }

    if (head.Code == HeadCodes.Water)
      WaterFacilityFrom = facilityFrom;
    Touch();
  }

  /// The water connection fee sets the facility date when the file has none yet.
  public void TakeFacility(TaxHead head, DateOnly from)
  {
    if (HeadValue(head.Id)?.FacilityFrom is null)
      SetHeadValue(head, from, HeadValue(head.Id)?.Quantity);
  }

  // ---- overrides ----

  /// The override of a head in force on a date (the latest started).
  public TaxFileOverride? OverrideFor(TaxHeadId headId, DateOnly date) =>
      _overrides.Where(o => o.HeadId == headId && o.Covers(date)).OrderByDescending(o => o.EffectiveFrom).FirstOrDefault();

  /// A per-head override from a date: a fixed special amount or a special policy (SPECIAL), or a floor (MINIMUM). An
  /// open override of the head that started earlier ends the day before.
  public TaxFileOverride AddOverride(TaxHead head, OverrideMode mode, SpecialPolicy? policy, decimal? amount, bool followsIncrease, string reason, string? reference,
      DateOnly effectiveFrom, Guid actor, DateTime now)
  {
    if (!head.OverrideAllowed)
      throw new DomainException($"{head.Name} does not allow an override.");
    if (policy is not null)
    {
      if (mode != OverrideMode.Special)
        throw new DomainException("A special policy replaces the computed amount; it cannot be a minimum.");
      if (!policy.IsActive)
        throw new DomainException($"\"{policy.Name}\" is not active.");
      if (policy.HeadId != head.Id)
        throw new DomainException($"\"{policy.Name}\" is a policy for another head, not for {head.Name}.");
      if (!policy.AppliesToCategory(TaxCategory))
        throw new DomainException($"\"{policy.Name}\" is not for {(TaxCategory == TaxCategory.Com ? "commercial" : "residential")} properties.");
      amount = null;
    }
    else if (amount is not > 0)
    {
      throw new DomainException("Give the amount per year.");
    }

    if (_overrides.Any(o => o.HeadId == head.Id && o.EffectiveFrom >= effectiveFrom))
      throw new DomainException($"{head.Name} already has an override from {effectiveFrom:yyyy-MM-dd} or later. End it, or start this one later.");
    foreach (var open in _overrides.Where(o => o.HeadId == head.Id && o.EffectiveTo is null))
      open.End(effectiveFrom.AddDays(-1));

    var created = TaxFileOverride.Create(TaxFileOverrideId.New(), Id, head.Id, mode, policy?.Id, amount, followsIncrease, reason, reference, effectiveFrom, actor, now);
    _overrides.Add(created);
    Touch();
    return created;
  }

  public void EndOverride(TaxFileOverrideId id, DateOnly lastDay)
  {
    var ov = _overrides.FirstOrDefault(o => o.Id == id) ?? throw new DomainException("The override was not found on this file.");
    ov.End(lastDay);
    Touch();
  }

  /// An override set from a later date by mistake is taken back before it is in force (it never charged anything); an
  /// open override of the head it ended runs on. Returns the voided override.
  public TaxFileOverride VoidOverride(TaxFileOverrideId id, DateOnly today)
  {
    var ov = _overrides.FirstOrDefault(o => o.Id == id) ?? throw new DomainException("The override was not found on this file.");
    if (ov.EffectiveFrom <= today)
      throw new DomainException($"The override is in force since {ov.EffectiveFrom:dd'/'MM'/'yyyy}; end it instead.");
    if (_overrides.Any(o => o.HeadId == ov.HeadId && o.EffectiveFrom > ov.EffectiveFrom))
      throw new DomainException("A later override of the head follows it; take that one back first.");
    var previous = _overrides.Where(o => o.HeadId == ov.HeadId && o.EffectiveFrom < ov.EffectiveFrom).OrderByDescending(o => o.EffectiveFrom).FirstOrDefault();
    previous?.Reopen(ov.EffectiveFrom.AddDays(-1));
    _overrides.Remove(ov);
    Touch();
    return ov;
  }

  // ---- litigation holds ----

  public LitigationHold? HoldOn(DateOnly date) => _holds.FirstOrDefault(h => h.SuspendsDemand && h.Covers(date));

  /// Days under a hold that stops the completion clock, between two dates (both included).
  public int HoldDaysBetween(DateOnly from, DateOnly to)
  {
    var days = 0;
    foreach (var hold in _holds.Where(h => h.ExcludesCompletionClock))
    {
      var start = hold.StartedOn > from ? hold.StartedOn : from;
      var end = hold.EndedOn is null || hold.EndedOn.Value > to ? to : hold.EndedOn.Value;
      if (end >= start)
        days += end.DayNumber - start.DayNumber + 1;
    }
    return days;
  }

  public LitigationHold AddHold(string reason, string? reference, Guid? sourceLitigationId, DateOnly startedOn, bool suspendsDemand, bool excludesCompletionClock)
  {
    if (_holds.Any(h => h.EndedOn is null))
      throw new DomainException("The file is already on hold. End that hold first.");
    if (_holds.Any(h => h.EndedOn is { } ended && ended >= startedOn))
      throw new DomainException("A hold cannot start before an earlier hold ended.");
    var hold = LitigationHold.Create(LitigationHoldId.New(), Id, reason, reference, sourceLitigationId, startedOn, suspendsDemand, excludesCompletionClock);
    _holds.Add(hold);
    Touch();
    return hold;
  }

  public void EndHold(LitigationHoldId id, DateOnly endedOn)
  {
    var hold = _holds.FirstOrDefault(h => h.Id == id) ?? throw new DomainException("The hold was not found on this file.");
    hold.End(endedOn);
    Touch();
  }

  /// A hold put from a later date by mistake is taken back before it begins (it never suspended anything).
  public LitigationHold VoidHold(LitigationHoldId id, DateOnly today)
  {
    var hold = _holds.FirstOrDefault(h => h.Id == id) ?? throw new DomainException("The hold was not found on this file.");
    if (hold.StartedOn <= today)
      throw new DomainException($"The hold is in force since {hold.StartedOn:dd'/'MM'/'yyyy}; give it its last day instead.");
    _holds.Remove(hold);
    Touch();
    return hold;
  }

  // ---- agreements ----

  public AgreementSchedule Agreement(AgreementScheduleId id) =>
      _agreements.FirstOrDefault(a => a.Id == id) ?? throw new DomainException("The agreement was not found on this file.");

  /// Annual rent of a year: the RENTAL agreements in force during it.
  public IReadOnlyList<AgreementSchedule> RentalsFor(int fiscalYear) =>
      _agreements.Where(a => a.Kind == AgreementKind.Rental && a.RunsDuring(fiscalYear)).ToList();
}

/// What Taxation keeps of a property, read from the Property service at the moment of use.
public sealed record PropertySnapshot(
  Guid PropertyId,
  string PropertyCode,
  string PropertyName,
  bool IsActive,
  Guid? TownId,
  string? TownCode,
  string? TownName,
  Guid? PropertyTypeId,
  string? PropertyTypeCode,
  string? PropertyStatusCode,
  string? ClassificationCode,
  string? AddressLine,
  decimal? LandAreaSqft,
  decimal? LandAreaEntered,
  string? LandAreaUnitCode,
  decimal? RegularizedAreaSqft,
  decimal? CoveredAreaSqft,
  CoveredAreaSource CoveredAreaSource,
  Guid? SourceMeasurementId,
  Guid? SourceBuildingPlanId,
  DateOnly? BpaDate,
  Guid? CompletionPlanId,
  DateOnly? CompletionDate,
  HoldingKind HoldingKind,
  IReadOnlyList<PayerSnapshot> Payers,
  IReadOnlyList<AgreementSnapshot> Agreements,
  string? SourceVersion);

public sealed record PayerSnapshot(
  Guid OwnerId,
  string OwnerCode,
  PayerRole Role,
  PayerSource SourceType,
  Guid SourceId,
  decimal? SharePct,
  string Name,
  string? FatherHusbandName,
  string? CnicNtn,
  string? Mobile,
  string? PostalAddress,
  DateOnly? EffectiveFrom)
{
  public bool Matches(TaxFilePayer payer) => payer.OwnerId == OwnerId && payer.SourceType == SourceType && payer.SourceId == SourceId;
}

public sealed record AgreementSnapshot(
  AgreementKind Kind,
  Guid SourceId,
  string SourceNo,
  decimal BaseAmount,
  AmountFrequency Frequency,
  DateOnly StartDate,
  DateOnly? EndDate,
  int? TermYears,
  decimal? StepPct,
  bool IsActive);

public sealed record TaxFileDetails(
  int? Rooms,
  DateOnly? RegisteredOn,
  UpliftRateBasis? UpliftRateBasis,
  decimal? UpliftPct,
  string? OldReferenceNo,
  string? PlotNo,
  string? Remarks,
  string? ExemptionReason = null);
