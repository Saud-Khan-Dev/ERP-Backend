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
  public string? Remarks { get; private set; }

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

  /// The clerk's "add owner" act on a property that may already have owners: the owner may not already
  /// hold a share, the shares held stay within 100%, and the new period may not overlap the owner's history.
  public static PropertyOwnership AddOwner(
      OwnershipId id,
      PropertyOwner owner,
      TenureType tenureType,
      decimal sharePct,
      DateOnly effectiveFrom,
      TransferType? acquisitionTransferType,
      string? referenceNo,
      string? remarks,
      OwnershipRecord record)
  {
    ArgumentNullException.ThrowIfNull(owner);
    ArgumentNullException.ThrowIfNull(record);

    EnsureCanOpen(owner, sharePct, effectiveFrom, record);

    return Register(id, record.Property, owner, tenureType, sharePct, effectiveFrom, acquisitionTransferType,
      referenceNo, remarks, record.Today);
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

  /// The clerk's "end ownership" act (e.g. surrender to GDA), without a transfer. Refused in the future, on a
  /// deactivated property, while a charge is registered on the share or the property, and while the share
  /// is being given away in a pending transfer.
  public void EndHolding(DateOnly effectiveTo, string? remarks, OwnershipRecord record)
  {
    ArgumentNullException.ThrowIfNull(record);
    EnsureOn(record.Property);

    if (!IsCurrent)
      throw new DomainException("This ownership has already ended.");

    if (effectiveTo > record.Today)
      throw new DomainException($"An ownership cannot end in the future ({effectiveTo:yyyy-MM-dd}); today is {record.Today:yyyy-MM-dd}.");

    EnsureCanEnd(record, "the ownership is ended");
    End(effectiveTo, remarks);
  }

  /// A share cannot be ended while a charge sits on it or on the whole property, or while it is being
  /// given away in a pending transfer. `act` completes "release it before …".
  internal void EnsureCanEnd(OwnershipRecord record, string act)
  {
    var name = record.NameOf(OwnerId);

    if (record.ChargeOn(this) is { } charge)
      throw new DomainException($"{name}'s {OwnershipSharePct:0.####}% is under {charge.Describe()}; release it before {act}.");

    if (record.PropertyCharge is { } propertyCharge)
      throw new DomainException($"{record.Property.PropertyCode.Value} is under {propertyCharge.Describe()} on the whole property; release it before {act}.");

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

  /// Rules shared by "add owner" and "confirm the allottee": one current share per owner, at most 100%,
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
