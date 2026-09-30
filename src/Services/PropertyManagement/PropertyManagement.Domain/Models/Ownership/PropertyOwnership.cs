/// One owner holding a share of one property for a period.
///
/// History, never overwrite (schema guide, rule 2): when ownership changes the row is closed
/// (effective_to + status ENDED) and new rows are inserted. Current owners are the ACTIVE rows, and
/// their shares may not add up to more than 100% (rule 1, see OwnershipShares).
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
  /// Set when ownership arises from an allotment (property_allotment arrives in phase 2).
  public Guid? AcquiredViaAllotmentId { get; private set; }
  /// Mutation / registry / deed no.
  public string? ReferenceNo { get; private set; }
  public string? Remarks { get; private set; }

  /// Counts toward the 100% total and toward "current owners".
  public bool IsCurrent => OwnershipStatus == OwnershipStatus.Active;

  public static PropertyOwnership Register(
      OwnershipId id,
      Property property,
      PropertyOwner owner,
      TenureType tenureType,
      decimal sharePct,
      DateOnly effectiveFrom,
      TransferType? acquisitionTransferType,
      string? referenceNo,
      string? remarks)
  {
    ArgumentNullException.ThrowIfNull(property);
    ArgumentNullException.ThrowIfNull(owner);
    ArgumentNullException.ThrowIfNull(tenureType);
    property.EnsureActive();
    owner.EnsureActive();
    tenureType.EnsureActive();
    acquisitionTransferType?.EnsureActive();

    return New(id, property.Id, owner.Id, tenureType.Id, sharePct, effectiveFrom,
      acquisitionTransferType?.Id, acquiredViaTransferId: null, referenceNo, remarks);
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
    ReferenceNo = Guard.Text(referenceNo, 100, "Reference no."),
    Remarks = Guard.Text(remarks, 4000, "Remarks")
  };

  /// Closes the period. The row stays as ownership history.
  public void End(DateOnly effectiveTo, string? remarks = null)
  {
    if (OwnershipStatus == OwnershipStatus.Ended)
      throw new DomainException("This ownership has already ended.");

    if (effectiveTo < EffectiveFrom)
      throw new DomainException($"Ownership cannot end before it started ({EffectiveFrom:yyyy-MM-dd}).");

    EffectiveTo = effectiveTo;
    OwnershipStatus = OwnershipStatus.Ended;

    if (!string.IsNullOrWhiteSpace(remarks))
      Remarks = Guard.Text(remarks, 4000, "Remarks");
  }

  /// A disputed share stops counting as current until the dispute is resolved.
  public void MarkDisputed(string? remarks)
  {
    if (OwnershipStatus != OwnershipStatus.Active)
      throw new DomainException("Only an active ownership can be marked as disputed.");

    OwnershipStatus = OwnershipStatus.Disputed;
    Remarks = Guard.Text(remarks, 4000, "Remarks") ?? Remarks;
  }

  /// Back to Active; the caller re-checks the 100% total first (OwnershipShares).
  public void ResolveDispute(string? remarks)
  {
    if (OwnershipStatus != OwnershipStatus.Disputed)
      throw new DomainException("Only a disputed ownership can be resolved.");

    OwnershipStatus = OwnershipStatus.Active;
    Remarks = Guard.Text(remarks, 4000, "Remarks") ?? Remarks;
  }
}

/// Rule 1: for one property, active ownership shares may total at most 100%.
public static class OwnershipShares
{
  public const decimal Whole = 100m;

  public static decimal Total(IEnumerable<PropertyOwnership> ownerships) =>
      ownerships.Where(o => o.IsCurrent).Sum(o => o.OwnershipSharePct);

  public static void EnsureRoomFor(IEnumerable<PropertyOwnership> currentOwnerships, decimal addingPct)
  {
    var total = Total(currentOwnerships);

    if (total + addingPct > Whole)
      throw new DomainException(
        $"Active ownership shares would total {total + addingPct:0.####}%. Only {Whole - total:0.####}% of this property is unallocated.");
  }
}
