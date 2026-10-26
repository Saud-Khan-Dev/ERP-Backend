/// One owner holding a share of one property for a period.
///
/// History, never overwrite (schema guide, rule 2): when ownership changes the row is closed
/// (effective_to + status ENDED) and new rows are inserted. The current holders are the ACTIVE and DISPUTED
/// rows: a disputed share is still held, so it counts toward the 100% total (rule 1, see OwnershipShares),
/// but only an ACTIVE share can be transferred.
public class PropertyOwnership : Aggregate<OwnershipId>
{
  public PropertyId PropertyId { get; private set; } = default!;
  public OwnerId OwnerId { get; private set; } = default!;
  /// How the property is held (Owned, Leased ...) — separate from who the owner is (owner_type).
  public MasterId TenureTypeId { get; private set; } = default!;
  public decimal OwnershipSharePct { get; private set; }
  public DateOnly EffectiveFrom { get; private set; }
  /// How well EffectiveFrom is known (old records: "March 1972", "1972", "about 1972"). A row opened by a transfer
  /// takes the transfer's.
  public DatePrecision EffectiveFromPrecision { get; private set; }
  /// Null while current.
  public DateOnly? EffectiveTo { get; private set; }
  public OwnershipStatus OwnershipStatus { get; private set; }
  /// How this owner acquired the share (transfer_type).
  public MasterId? AcquisitionTransferTypeId { get; private set; }
  public TransferId? AcquiredViaTransferId { get; private set; }
  /// Set when GDA confirms an allottee as legal owner (allotment is not ownership by itself).
  public AllotmentId? AcquiredViaAllotmentId { get; private set; }
  /// Mutation / registry / deed no.
  public string? ReferenceNo { get; private set; }
  /// The order or reference under which a share was ended by hand, when the register still had an "end ownership"
  /// act (removed 2026-10-26: a share now ends only through a transfer or an allotment's cancellation / surrender).
  /// Kept for the rows it was written on.
  public string? EndReferenceNo { get; private set; }
  public string? Remarks { get; private set; }

  public const int ReferenceMaxLength = 100;

  /// Held now: counts toward the 100% total and toward "current owners". A disputed share is still held.
  public bool IsCurrent => OwnershipStatus is OwnershipStatus.Active or OwnershipStatus.Disputed;

  /// Only an undisputed current share can be given away in a transfer.
  public bool IsTransferable => OwnershipStatus == OwnershipStatus.Active;

  /// Records a share directly. Today, when given, refuses a start date in the future.
  public static PropertyOwnership Register(
      OwnershipId id,
      Property property,
      PropertyOwner owner,
      TenureType tenureType,
      decimal sharePct,
      DateOnly effectiveFrom,
      TransferType? acquisitionTransferType,
      string? referenceNo,
      string? remarks,
      DateOnly? today = null)
  {
    ArgumentNullException.ThrowIfNull(property);
    ArgumentNullException.ThrowIfNull(owner);
    ArgumentNullException.ThrowIfNull(tenureType);
    property.EnsureActive();
    owner.EnsureActive();
    tenureType.EnsureActive();
    acquisitionTransferType?.EnsureActive();

    if (today is { } day && effectiveFrom > day)
      throw new DomainException($"An ownership cannot start in the future ({effectiveFrom:yyyy-MM-dd}); today is {day:yyyy-MM-dd}.");

    return New(id, property.Id, owner.Id, tenureType.Id, sharePct, effectiveFrom,
      acquisitionTransferType?.Id, acquiredViaTransferId: null, acquiredViaAllotmentId: null, referenceNo, remarks);
  }

  /// One of the first owners entered on a property nobody holds: who, the share, how it is held and since when.
  public sealed record FirstOwner(
      PropertyOwner Owner,
      TenureType TenureType,
      decimal SharePct,
      DateOnly EffectiveFrom,
      TransferType? AcquisitionTransferType = null,
      string? ReferenceNo = null,
      string? Remarks = null,
      DatePrecision Precision = DatePrecision.Exact);

  /// The "add the first owner(s)" act, also used when a property is registered with its owners: the owners of a
  /// property that NOBODY holds now, entered in one go (joint owners together) with shares that add up to exactly 100%.
  /// A partial first entry is refused: the rest could never be added, since once anyone holds a property a new owner
  /// comes in only through a transfer (started and approved, or a completed one recorded from the paper register),
  /// which keeps the chain of title unbroken. Refused while an allotment is in force (the allottee becomes owner through
  /// "Confirm as owner"), for an owner listed twice, a start date in the future or one that overlaps the owner's own
  /// earlier entries on the property. Who may do it (the clerk on a property with no holder history, an approver
  /// otherwise) is decided by the handler.
  public static IReadOnlyList<PropertyOwnership> RegisterFirstOwners(IReadOnlyList<FirstOwner> owners, OwnershipRecord record)
  {
    ArgumentNullException.ThrowIfNull(owners);
    ArgumentNullException.ThrowIfNull(record);
    var code = record.Property.PropertyCode.Value;

    if (record.Current.Count > 0)
      throw new DomainException(
        $"{code} is already registered in the name of {HolderList(record)}. A new owner can only come in through a transfer: "
        + "use \"Start transfer\", or \"Record a completed transfer\" for one that already happened on paper.");

    if (record.AllotmentInForce is { } allotted)
      throw new DomainException(
        $"{code} is allotted to {allotted.AllotteeName} ({allotted.Allotment.AllotmentNo.Value}); "
        + "while the allotment is in force the plot gets an owner only by confirming the allottee as owner.");

    if (owners.Count == 0)
      throw new DomainException("Give at least one owner, with the share they hold.");

    if (owners.GroupBy(o => o.Owner.Id).FirstOrDefault(g => g.Count() > 1) is { } repeated)
    {
      var owner = repeated.First().Owner;
      throw new DomainException(
        $"{owner.OwnerName.Value} ({owner.OwnerCode.Value}) is listed more than once. Give each owner one row with their whole share.");
    }

    var total = owners.Sum(o => o.SharePct);
    if (total > OwnershipShares.Whole)
      throw new DomainException(
        $"The shares add up to {total:0.####}%; together the owners hold exactly {OwnershipShares.Whole:0}% of the property. Lower the shares so they add up to {OwnershipShares.Whole:0}%.");
    if (total < OwnershipShares.Whole)
      throw new DomainException(
        $"The shares add up to {total:0.####}%, so {OwnershipShares.Whole - total:0.####}% would have no owner. Enter every owner now, with shares that add up to "
        + $"exactly {OwnershipShares.Whole:0}%: once the property is held, another owner can only come in through a transfer.");

    foreach (var o in owners)
      OwnershipHistory.EnsureCanStart(o.Owner.OwnerName.Value, record.RowsOf(o.Owner.Id), o.EffectiveFrom, "the new share");

    // a share registered as it stands today needs a holder with a CNIC (or the estate of one who has died); people
    // from the old records hold shares only inside an ownership history being played forward
    if (!record.ReplayingHistory)
      foreach (var o in owners)
        o.Owner.EnsureCanHoldToday();

    return owners
      .Select(o =>
      {
        var row = Register(OwnershipId.New(), record.Property, o.Owner, o.TenureType, o.SharePct, o.EffectiveFrom,
          o.AcquisitionTransferType, o.ReferenceNo, o.Remarks, record.Today);
        row.EffectiveFromPrecision = o.Precision;
        return row;
      })
      .ToList();
  }

  /// A row opened by a transfer from the old records starts as precisely as the transfer's date is known.
  internal void MarkDatePrecision(DatePrecision precision) => EffectiveFromPrecision = precision;

  /// The transfer that opened / closed this row was re-dated (PropertyTransfer.CorrectHistory checks the order).
  internal void MoveStart(DateOnly effectiveFrom) => EffectiveFrom = effectiveFrom;

  internal void MoveEnd(DateOnly effectiveTo) => EffectiveTo = effectiveTo;

  /// An ownership history entry that ends with the property's present holders explains how this holding (entered from
  /// the register, nothing behind it) came about: it takes the acquisition the history worked out for the same owner,
  /// share and start - the transfer that brought it, or the earlier holding it is the remainder of.
  internal void AdoptHistoryOf(PropertyOwnership replayed)
  {
    if (AcquiredViaTransferId is not null || AcquiredViaAllotmentId is not null)
      throw new DomainException("This holding already says how it was acquired.");

    AcquiredViaTransferId = replayed.AcquiredViaTransferId;
    AcquisitionTransferTypeId = replayed.AcquisitionTransferTypeId ?? AcquisitionTransferTypeId;
    ReferenceNo ??= replayed.ReferenceNo;
    EffectiveFromPrecision = replayed.EffectiveFromPrecision;
    Remarks ??= replayed.Remarks;
  }

  /// An approving officer corrects how a holding was entered from the old papers: how it was acquired, the deed /
  /// mutation no., remarks - and, for a holding that no transfer or allotment opened (a first owner), its start date
  /// and how well that date is known. Rows a transfer opened start on the transfer's date: correct the transfer instead.
  public void CorrectHistory(
      TransferType? acquisitionTransferType,
      string? referenceNo,
      string? remarks,
      DateOnly effectiveFrom,
      DatePrecision precision,
      OwnershipRecord record)
  {
    ArgumentNullException.ThrowIfNull(record);
    acquisitionTransferType?.EnsureActive();
    var opener = AcquiredViaTransferId is not null ? "transfer" : AcquiredViaAllotmentId is not null ? "allotment" : null;

    if (effectiveFrom != EffectiveFrom || precision != EffectiveFromPrecision)
    {
      if (opener is not null || (Remarks ?? "").StartsWith("Remainder after", StringComparison.Ordinal))
        throw new DomainException(
          $"This holding started with the {opener ?? "transfer"} that created it, so its start date is that {opener ?? "transfer"}'s date - correct the {opener ?? "transfer"} instead.");
      if (effectiveFrom > record.Today)
        throw new DomainException($"A holding cannot start in the future ({effectiveFrom:yyyy-MM-dd}); today is {record.Today:yyyy-MM-dd}.");
      if (EffectiveTo is { } end && effectiveFrom > end)
        throw new DomainException($"This holding ended on {end:yyyy-MM-dd}; it cannot start after that.");

      // the other entries of the same owner on the property keep their order, one entry per day
      var others = record.RowsOf(OwnerId).Where(r => r.Id != Id).ToList();
      if (others.Any(r => r.EffectiveFrom == effectiveFrom))
        throw new DomainException($"{record.NameOf(OwnerId)} already has a holding on this property starting {effectiveFrom:yyyy-MM-dd}.");
      if (others.Any(r => r.EffectiveFrom < EffectiveFrom && (r.EffectiveTo ?? DateOnly.MaxValue) > effectiveFrom))
        throw new DomainException($"The new start date overlaps {record.NameOf(OwnerId)}'s earlier holding on this property.");
    }

    AcquisitionTransferTypeId = acquisitionTransferType?.Id;
    ReferenceNo = Guard.Text(referenceNo, ReferenceMaxLength, "Reference no.");
    Remarks = Guard.Text(remarks, 4000, "Remarks");
    EffectiveFrom = effectiveFrom;
    EffectiveFromPrecision = precision;
  }

  /// "A (60%) and B (40%)": the current holders, largest share first.
  private static string HolderList(OwnershipRecord record)
  {
    var names = record.Current
      .GroupBy(o => o.OwnerId)
      .Select(g => (Name: record.NameOf(g.Key), Pct: g.Sum(o => o.OwnershipSharePct)))
      .OrderByDescending(h => h.Pct).ThenBy(h => h.Name)
      .Select(h => $"{h.Name} ({h.Pct:0.####}%)")
      .ToList();
    return names.Count == 1 ? names[0] : $"{string.Join(", ", names.Take(names.Count - 1))} and {names[^1]}";
  }

  /// GDA confirms the allottee as legal owner: the ownership points back at the allotment.
  /// The allotment's own rules (in force, not confirmed yet, dates) are checked by PropertyAllotment.ConfirmOwnership.
  public static PropertyOwnership FromAllotment(
      OwnershipId id,
      Property property,
      PropertyAllotment allotment,
      PropertyOwner allottee,
      TenureType tenureType,
      decimal sharePct,
      DateOnly effectiveFrom,
      string? referenceNo,
      string? remarks,
      DateOnly? today = null)
  {
    ArgumentNullException.ThrowIfNull(property);
    ArgumentNullException.ThrowIfNull(allotment);
    ArgumentNullException.ThrowIfNull(allottee);

    if (allotment.PropertyId != property.Id)
      throw new DomainException("The allotment belongs to another property.");

    if (allotment.AllotteeOwnerId != allottee.Id)
      throw new DomainException("Only the allottee can be confirmed as owner through this allotment.");

    var ownership = Register(id, property, allottee, tenureType, sharePct, effectiveFrom, null, referenceNo, remarks, today);
    ownership.AcquiredViaAllotmentId = allotment.Id;
    return ownership;
  }

  internal static PropertyOwnership New(
      OwnershipId id,
      PropertyId propertyId,
      OwnerId ownerId,
      MasterId tenureTypeId,
      decimal sharePct,
      DateOnly effectiveFrom,
      MasterId? acquisitionTransferTypeId,
      TransferId? acquiredViaTransferId,
      AllotmentId? acquiredViaAllotmentId,
      string? referenceNo,
      string? remarks) => new()
  {
    Id = id,
    PropertyId = propertyId,
    OwnerId = ownerId,
    TenureTypeId = tenureTypeId,
    OwnershipSharePct = Guard.SharePct(sharePct, "Ownership share"),
    EffectiveFrom = effectiveFrom,
    OwnershipStatus = OwnershipStatus.Active,
    AcquisitionTransferTypeId = acquisitionTransferTypeId,
    AcquiredViaTransferId = acquiredViaTransferId,
    AcquiredViaAllotmentId = acquiredViaAllotmentId,
    ReferenceNo = Guard.Text(referenceNo, 100, "Reference no."),
    Remarks = Guard.Text(remarks, 4000, "Remarks")
  };

  /// Closes the period. The row stays as ownership history; the remark is added to the row's remarks.
  public void End(DateOnly effectiveTo, string? remarks = null)
  {
    if (OwnershipStatus == OwnershipStatus.Ended)
      throw new DomainException("This ownership has already ended.");

    if (effectiveTo < EffectiveFrom)
      throw new DomainException($"Ownership cannot end before it started ({EffectiveFrom:yyyy-MM-dd}).");

    EffectiveTo = effectiveTo;
    OwnershipStatus = OwnershipStatus.Ended;
    AddRemark(remarks);
  }

  /// A share cannot be ended while a charge sits on it or on the whole property, or while it is being
  /// given away in a pending transfer. `act` completes "release it before …".
  internal void EnsureCanEnd(OwnershipRecord record, string act)
  {
    var name = record.NameOf(OwnerId);

    if (record.ChargeOn(this) is { } charge)
      throw new DomainException(charge.Status == EncumbranceStatus.Enforced
        ? $"{name}'s {OwnershipSharePct:0.####}% is under {charge.Describe()}, which was enforced; the share can only be transferred to {charge.HolderName}."
        : $"{name}'s {OwnershipSharePct:0.####}% is under {charge.Describe()}; release it before {act}.");

    if (record.PropertyCharge is { } propertyCharge)
      throw new DomainException(propertyCharge.Status == EncumbranceStatus.Enforced
        ? $"{record.Property.PropertyCode.Value} is under {propertyCharge.Describe()} on the whole property, which was enforced; shares can only be transferred to {propertyCharge.HolderName}."
        : $"{record.Property.PropertyCode.Value} is under {propertyCharge.Describe()} on the whole property; release it before {act}.");

    var pending = record.PendingTransfersGivenBy(OwnerId).ToList();
    if (pending.Count > 0)
      throw new DomainException(
        $"{name}'s share is being transferred in {string.Join(", ", pending.Select(t => t.TransferNo.Value))}; cancel or approve that first.");
  }

  /// A disputed share is still held (it keeps counting toward the 100%) but cannot be transferred.
  public void MarkDisputed(Property property, string? remarks)
  {
    EnsureOn(property);

    if (OwnershipStatus != OwnershipStatus.Active)
      throw new DomainException("Only an active ownership can be marked as disputed.");

    OwnershipStatus = OwnershipStatus.Disputed;
    AddRemark(remarks);
  }

  /// Back to Active: the share can be transferred again.
  public void ResolveDispute(Property property, string? remarks)
  {
    EnsureOn(property);

    if (OwnershipStatus != OwnershipStatus.Disputed)
      throw new DomainException("Only a disputed ownership can be resolved.");

    OwnershipStatus = OwnershipStatus.Active;
    AddRemark(remarks);
  }

  /// Checks the property passed in is this row's property and is still on the register.
  internal void EnsureOn(Property property)
  {
    ArgumentNullException.ThrowIfNull(property);

    if (property.Id != PropertyId)
      throw new DomainException("The ownership belongs to another property.");

    property.EnsureActive();
  }

  /// Rules of "confirm the allottee": one current share per owner, at most 100%,
  /// and the new period starts after the owner's earlier history on this property.
  internal static void EnsureCanOpen(PropertyOwner owner, decimal sharePct, DateOnly effectiveFrom, OwnershipRecord record)
  {
    var code = record.Property.PropertyCode.Value;
    var name = owner.OwnerName.Value;
    var held = record.CurrentRowsOf(owner.Id).ToList();

    if (held.Count > 0)
      throw new DomainException(
        $"{name} already holds {held.Sum(o => o.OwnershipSharePct):0.####}% of {code} (since {OwnershipHistory.HoldingSince(held[0], record.Ownerships):yyyy-MM-dd}). Record a transfer to change the share.");

    OwnershipShares.EnsureRoomFor(record.Current, sharePct);
    OwnershipHistory.EnsureCanStart(name, record.RowsOf(owner.Id), effectiveFrom, "the new share");
  }

  private void AddRemark(string? remarks)
  {
    var added = Guard.Text(remarks, 4000, "Remarks");
    if (added is null)
      return;

    var combined = Remarks is null ? added : $"{Remarks}\n{added}";
    Remarks = combined.Length > 4000 ? combined[^4000..] : combined;
  }
}

/// Rule 1: for one property, the shares held (ACTIVE + DISPUTED) may total at most 100%.
public static class OwnershipShares
{
  public const decimal Whole = 100m;

  public static decimal Total(IEnumerable<PropertyOwnership> ownerships) =>
      ownerships.Where(o => o.IsCurrent).Sum(o => o.OwnershipSharePct);

  public static void EnsureRoomFor(IEnumerable<PropertyOwnership> currentOwnerships, decimal addingPct)
  {
    var total = Total(currentOwnerships);

    if (total >= Whole)
      throw new DomainException(
        $"Current owners already hold {Whole:0.####}% of this property; nothing is unallocated. Record a transfer to move a share instead.");

    if (total + addingPct > Whole)
      throw new DomainException(
        $"Shares held would total {total + addingPct:0.####}%. Only {Whole - total:0.####}% of this property is unallocated.");
  }
}

/// Reading an owner's history on one property.
public static class OwnershipHistory
{
  /// The start of the owner's continuous holding: walks back through the owner's earlier rows on the same
  /// property that ended exactly where the next one began (a transfer that changed the share, a remainder
  /// after a partial sale), so a row opened by today's transfer still shows the owner held since 2015.
  public static DateOnly HoldingSince(PropertyOwnership row, IEnumerable<PropertyOwnership> ownerships)
  {
    ArgumentNullException.ThrowIfNull(row);

    var earlier = ownerships
        .Where(o => o.PropertyId == row.PropertyId && o.OwnerId == row.OwnerId && o.Id != row.Id && o.EffectiveTo != null)
        .ToList();

    var visited = new HashSet<OwnershipId> { row.Id };
    var since = row.EffectiveFrom;

    while (earlier.Where(o => o.EffectiveTo == since && !visited.Contains(o.Id)).OrderBy(o => o.EffectiveFrom).FirstOrDefault() is { } previous)
    {
      visited.Add(previous.Id);
      since = previous.EffectiveFrom;
    }

    return since;
  }

  /// A new period for this owner must start after every earlier entry of the owner on the property began
  /// (one entry per owner per day) and not before the latest one ended (no overlapping periods).
  public static void EnsureCanStart(string ownerName, IEnumerable<PropertyOwnership> ownerRows, DateOnly date, string what)
  {
    var rows = ownerRows.ToList();
    if (rows.Count == 0)
      return;

    var latest = rows.OrderByDescending(o => o.EffectiveFrom).First();
    if (date <= latest.EffectiveFrom)
      throw new DomainException(latest.IsCurrent
        ? $"{ownerName} holds this share only since {latest.EffectiveFrom:yyyy-MM-dd}; date {what} after that."
        : $"{ownerName} already has an ownership entry starting {latest.EffectiveFrom:yyyy-MM-dd}; date {what} after that.");

    var lastEnd = rows.Where(o => o.EffectiveTo != null).Max(o => o.EffectiveTo);
    if (lastEnd is { } end && date < end)
      throw new DomainException($"{ownerName}'s earlier ownership runs until {end:yyyy-MM-dd}; date {what} on or after that.");
  }
}
