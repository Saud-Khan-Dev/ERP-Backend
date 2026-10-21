/// One mortgage, lien or charge on the property (Act s.6(4)(c)). Released or enforced, never deleted.
///
/// While ACTIVE the charged share (or, without an ownership, every share) cannot be transferred or ended. ENFORCED
/// (the holder foreclosed) keeps it in place too, except for a transfer to the charge holder: approving that transfer
/// satisfies the charge (RELEASED, with the transfer as its release reference).
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

  /// Holds the charged share in place: ACTIVE, or ENFORCED until the share has gone to the charge holder.
  public bool HoldsShares => Status is EncumbranceStatus.Active or EncumbranceStatus.Enforced;

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
      string? remarks,
      DateOnly? today = null)
  {
    ArgumentNullException.ThrowIfNull(property);
    property.EnsureActive();
    EnsureStartNotFuture(startDate, today);

    var encumbrance = new PropertyEncumbrance { Id = id, PropertyId = property.Id, Status = EncumbranceStatus.Active };
    encumbrance.Apply(encumbranceType, ownership, holderName, holder, referenceNo, amount, startDate, endDate, remarks);
    return encumbrance;
  }

  public void Update(
      Property property,
      EncumbranceType encumbranceType,
      PropertyOwnership? ownership,
      string holderName,
      PropertyOwner? holder,
      string? referenceNo,
      decimal? amount,
      DateOnly startDate,
      DateOnly? endDate,
      string? remarks,
      DateOnly? today = null)
  {
    EnsureOn(property);
    EnsureActive();
    EnsureStartNotFuture(startDate, today);
    Apply(encumbranceType, ownership, holderName, holder, referenceNo, amount, startDate, endDate, remarks);
  }

  public void Release(Property property, DateOnly releaseDate, string? releaseReferenceNo, DateOnly today)
  {
    EnsureOn(property);
    EnsureActive();

    if (releaseDate < StartDate)
      throw new DomainException("An encumbrance cannot be released before it started.");

    if (releaseDate > today)
      throw new DomainException($"The release date cannot be in the future ({releaseDate:yyyy-MM-dd}); today is {today:yyyy-MM-dd}.");

    ReleaseDate = releaseDate;
    ReleaseReferenceNo = Guard.Text(releaseReferenceNo, 100, "Release reference no.");
    Status = EncumbranceStatus.Released;
  }

  /// The holder exercised the charge (e.g. the bank foreclosed). The share stays in place until it is transferred
  /// to the charge holder, so the holder must be a registered owner record.
  public void Enforce(Property property, string? remarks)
  {
    EnsureOn(property);
    EnsureActive();

    if (HolderOwnerId is null)
      throw new DomainException(
        $"Link {HolderName} to an owner record first (edit the charge): an enforced charge is settled by transferring the share to its holder.");

    Status = EncumbranceStatus.Enforced;
    Remarks = Guard.Text(remarks, 4000, "Remarks") ?? Remarks;
  }

  /// An approved transfer moved the enforced share to the charge holder: the charge is satisfied.
  internal void Satisfy(string transferNo, DateOnly date)
  {
    if (Status != EncumbranceStatus.Enforced)
      throw new DomainException("Only an enforced charge is settled by a transfer to its holder.");

    Status = EncumbranceStatus.Released;
    ReleaseDate = date < StartDate ? StartDate : date;
    ReleaseReferenceNo = transferNo;
    Remarks = RemarkText.Append(Remarks, $"Satisfied by transfer {transferNo} to {HolderName}.");
  }

  /// The share may move under this charge: never while ACTIVE; once ENFORCED, only to the charge holder.
  public bool AllowsTransferTo(IReadOnlyCollection<OwnerId> receivers) =>
      Status == EncumbranceStatus.Enforced && HolderOwnerId is not null && receivers.Count > 0 && receivers.All(r => r == HolderOwnerId);

  /// "the charge held by Bank of Khyber (ref MRT-1)", for messages that name the charge in the way.
  public string Describe() =>
      $"the charge held by {HolderName}{(ReferenceNo is null ? "" : $" (ref {ReferenceNo})")}";

  private static void EnsureStartNotFuture(DateOnly startDate, DateOnly? today)
  {
    if (today is { } day && startDate > day)
      throw new DomainException($"A charge cannot start in the future ({startDate:yyyy-MM-dd}); today is {day:yyyy-MM-dd}.");
  }

  private void EnsureOn(Property property)
  {
    ArgumentNullException.ThrowIfNull(property);

    if (property.Id != PropertyId)
      throw new DomainException("The encumbrance belongs to another property.");

    property.EnsureActive();
  }

  private void EnsureActive()
  {
    if (Status != EncumbranceStatus.Active)
      throw new DomainException($"This charge is {Status.ToString().ToLowerInvariant()} and can no longer change.");
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

    // a charge sits on a share that is held now; an ended row is history (also when an edit re-links it)
    if (ownership is not null && ownership.Id != OwnershipId && !ownership.IsCurrent)
      throw new DomainException(
        $"That ownership ended on {ownership.EffectiveTo:yyyy-MM-dd}; register the charge against the owner's current share.");

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
