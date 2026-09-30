/// One mortgage, lien or charge on the property (Act s.6(4)(c)). Released or enforced, never deleted.
public class PropertyEncumbrance : Aggregate<EncumbranceId>
{
  public PropertyId PropertyId { get; private set; } = default!;
  public MasterId EncumbranceTypeId { get; private set; } = default!;
  /// Which owner's interest is encumbered, when it is one owner's share rather than the whole property.
  public OwnershipId? OwnershipId { get; private set; }
  /// Bank / lender / lien holder.
  public string HolderName { get; private set; } = default!;
  /// The holder as a registered party, when they are one.
  public OwnerId? HolderOwnerId { get; private set; }
  public string? ReferenceNo { get; private set; }
  public decimal? Amount { get; private set; }
  public DateOnly StartDate { get; private set; }
  public DateOnly? EndDate { get; private set; }
  public DateOnly? ReleaseDate { get; private set; }
  public string? ReleaseReferenceNo { get; private set; }
  public EncumbranceStatus Status { get; private set; }
  public string? Remarks { get; private set; }

  public static PropertyEncumbrance Register(
      EncumbranceId id,
      Property property,
      EncumbranceType encumbranceType,
      PropertyOwnership? ownership,
      string holderName,
      PropertyOwner? holder,
      string? referenceNo,
      decimal? amount,
      DateOnly startDate,
      DateOnly? endDate,
      string? remarks)
  {
    ArgumentNullException.ThrowIfNull(property);
    property.EnsureActive();

    var encumbrance = new PropertyEncumbrance { Id = id, PropertyId = property.Id, Status = EncumbranceStatus.Active };
    encumbrance.Apply(encumbranceType, ownership, holderName, holder, referenceNo, amount, startDate, endDate, remarks);
    return encumbrance;
  }

  public void Update(
      EncumbranceType encumbranceType,
      PropertyOwnership? ownership,
      string holderName,
      PropertyOwner? holder,
      string? referenceNo,
      decimal? amount,
      DateOnly startDate,
      DateOnly? endDate,
      string? remarks)
  {
    EnsureActive();
    Apply(encumbranceType, ownership, holderName, holder, referenceNo, amount, startDate, endDate, remarks);
  }

  public void Release(DateOnly releaseDate, string? releaseReferenceNo)
  {
    EnsureActive();

    if (releaseDate < StartDate)
      throw new DomainException("An encumbrance cannot be released before it started.");

    ReleaseDate = releaseDate;
    ReleaseReferenceNo = Guard.Text(releaseReferenceNo, 100, "Release reference no.");
    Status = EncumbranceStatus.Released;
  }

  /// The holder exercised the charge (e.g. the bank foreclosed).
  public void Enforce(string? remarks)
  {
    EnsureActive();
    Status = EncumbranceStatus.Enforced;
    Remarks = Guard.Text(remarks, 4000, "Remarks") ?? Remarks;
  }

  private void EnsureActive()
  {
    if (Status != EncumbranceStatus.Active)
      throw new DomainException($"This encumbrance is {Status.ToString().ToLowerInvariant()} and can no longer change.");
  }

  private void Apply(
      EncumbranceType encumbranceType,
      PropertyOwnership? ownership,
      string holderName,
      PropertyOwner? holder,
      string? referenceNo,
      decimal? amount,
      DateOnly startDate,
      DateOnly? endDate,
      string? remarks)
  {
    ArgumentNullException.ThrowIfNull(encumbranceType);

    if (encumbranceType.Id != EncumbranceTypeId)
      encumbranceType.EnsureActive();

    if (ownership is not null && ownership.PropertyId != PropertyId)
      throw new DomainException("The encumbered ownership belongs to another property.");

    Guard.DateOrder(startDate, endDate, "Start date", "End date");

    EncumbranceTypeId = encumbranceType.Id;
    OwnershipId = ownership?.Id;
    HolderName = Guard.RequiredText(holderName, 200, "Holder name");
    HolderOwnerId = holder?.Id;
    ReferenceNo = Guard.Text(referenceNo, 100, "Reference no.");
    Amount = Guard.NotNegative(amount, "Amount");
    StartDate = startDate;
    EndDate = endDate;
    Remarks = Guard.Text(remarks, 4000, "Remarks");
  }
}
