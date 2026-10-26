/// One plot or building — the hub every other record hangs off.
///
/// Deliberately small (schema guide, "Property core"): no owner, area or lease columns. Those live in
/// child tables so one property can have many of each over time.
public class Property : Aggregate<PropertyId>
{
  public const int NameMaxLength = 200;

  /// PROP-00001. Issued once and never changes for the life of the property.
  public BusinessCode PropertyCode { get; private set; } = default!;
  public Name PropertyName { get; private set; } = default!;
  public MasterId TownId { get; private set; } = default!;
  public MasterId PropertyTypeId { get; private set; } = default!;
  /// Current status. Changed only through ChangeStatus so status history stays complete.
  public MasterId PropertyStatusId { get; private set; } = default!;
  public MasterId PropertyClassificationId { get; private set; } = default!;
  public string? AddressLine { get; private set; }
  /// Revenue record reference (khasra / survey number).
  public string? KhasraSurveyNo { get; private set; }
  public string? Description { get; private set; }
  public string? Remarks { get; private set; }
  /// When the property was first registered (with GDA or the revenue record) - often long before it was entered in
  /// this system (created_at). Null when nobody has recorded it.
  public DateOnly? RegisteredOn { get; private set; }
  public DatePrecision RegisteredOnPrecision { get; private set; }
  /// The original registration's reference (register / file / mutation no.).
  public string? RegistrationReference { get; private set; }
  public bool IsActive { get; private set; }
  /// Why and when the property was taken off the register (cleared when it is reactivated).
  public DateOnly? DeactivatedOn { get; private set; }
  public string? DeactivationReason { get; private set; }

  public static Property Create(
      PropertyId id,
      BusinessCode propertyCode,
      Name propertyName,
      Town town,
      PropertyType propertyType,
      PropertyStatus status,
      PropertyClassification classification,
      string? addressLine,
      string? khasraSurveyNo,
      string? description,
      string? remarks)
  {
    ArgumentNullException.ThrowIfNull(propertyCode);
    ArgumentNullException.ThrowIfNull(status);
    status.EnsureActive();

    var property = new Property { Id = id, PropertyCode = propertyCode, PropertyStatusId = status.Id, IsActive = true };
    property.Apply(propertyName, town, propertyType, classification, addressLine, khasraSurveyNo, description, remarks);
    return property;
  }

  /// Corrects the property's details. The classification cannot change while an allotment is in force: GDA allotted
  /// the plot as its own land, and a private classification would leave the allotment standing on land GDA does not hold.
  public void UpdateDetails(
      Name propertyName,
      Town town,
      PropertyType propertyType,
      PropertyClassification classification,
      string? addressLine,
      string? khasraSurveyNo,
      string? description,
      string? remarks,
      PropertyAllotment.InForce? allotmentInForce = null)
  {
    EnsureActive();
    ArgumentNullException.ThrowIfNull(classification);

    if (allotmentInForce is { } allotted && classification.Id != PropertyClassificationId)
      throw new DomainException(
        $"{PropertyCode.Value} is allotted to {allotted.AllotteeName} ({allotted.Allotment.AllotmentNo.Value}); its classification cannot change while the allotment is in force.");

    Apply(propertyName, town, propertyType, classification, addressLine, khasraSurveyNo, description, remarks);
  }

  /// Records (or corrects) when and under what reference the property was first registered, as the old papers give
  /// it. Not in the future, and not after the first holding on record began (a property is registered before anyone
  /// holds it in the register).
  public void SetOriginalRegistration(DateOnly? registeredOn, DatePrecision precision, string? reference, DateOnly? firstHoldingFrom, DateOnly today)
  {
    EnsureActive();

    if (registeredOn is { } on)
    {
      if (on > today)
        throw new DomainException($"The registration date cannot be in the future ({on:yyyy-MM-dd}); today is {today:yyyy-MM-dd}.");
      if (firstHoldingFrom is { } held && on > held)
        throw new DomainException(
          $"{PropertyCode.Value}'s first owner on record holds it from {held:yyyy-MM-dd}; it was registered on or before that date.");
    }

    RegisteredOn = registeredOn;
    RegisteredOnPrecision = registeredOn is null ? DatePrecision.Exact : precision;
    RegistrationReference = Guard.Text(reference, 100, "Registration reference");
  }

  /// Opening row of the status history, written when the property is registered.
  public PropertyStatusHistory OpenStatusHistory(DateOnly effectiveFrom, string? reason, DateOnly today)
  {
    EnsureNotFuture(effectiveFrom, today);
    return PropertyStatusHistory.Open(PropertyStatusHistoryId.New(), Id, PropertyStatusId, effectiveFrom, reason, referenceNo: null);
  }

  /// Moves the property to a new status: closes the current history row and returns the new one
  /// (schema guide, rule 2 — history is never overwritten). A status records what is, so it cannot start
  /// in the future, nor before the current one.
  public PropertyStatusHistory ChangeStatus(
      PropertyStatus newStatus,
      PropertyStatusHistory? currentHistory,
      DateOnly effectiveFrom,
      string? reason,
      string? referenceNo,
      DateOnly today)
  {
    ArgumentNullException.ThrowIfNull(newStatus);
    EnsureActive();
    newStatus.EnsureActive();
    EnsureNotFuture(effectiveFrom, today);

    if (newStatus.Id == PropertyStatusId)
      throw new DomainException($"The property is already '{newStatus.Name.Value}'.");

    if (currentHistory is not null)
    {
      if (currentHistory.PropertyId != Id)
        throw new DomainException("The status history row belongs to another property.");

      currentHistory.Close(effectiveFrom);
    }

    PropertyStatusId = newStatus.Id;
    return PropertyStatusHistory.Open(PropertyStatusHistoryId.New(), Id, newStatus.Id, effectiveFrom, reason, referenceNo);
  }

  /// Nothing is hard-deleted (schema guide): a retired property is deactivated, with the reason (entered twice, merged
  /// into another, acquired ...) and the day. What still runs on it is checked by the handler first.
  public void Deactivate(string? reason = null, DateOnly? today = null)
  {
    if (!IsActive)
      throw new DomainException($"Property {PropertyCode.Value} is already inactive.");

    IsActive = false;
    DeactivatedOn = today;
    DeactivationReason = Guard.Text(reason, 4000, "Reason");
  }

  public void Activate()
  {
    IsActive = true;
    DeactivatedOn = null;
    DeactivationReason = null;
  }

  /// Child records (measurements, ownership, leases, cases, documents ...) may only be added to or changed
  /// on an active property.
  public void EnsureActive()
  {
    if (!IsActive)
      throw new DomainException($"Property {PropertyCode.Value} is inactive. Reactivate it before changing its records.");
  }

  private static void EnsureNotFuture(DateOnly effectiveFrom, DateOnly today)
  {
    if (effectiveFrom > today)
      throw new DomainException("A status cannot start in the future.");
  }

  private void Apply(
      Name propertyName,
      Town town,
      PropertyType propertyType,
      PropertyClassification classification,
      string? addressLine,
      string? khasraSurveyNo,
      string? description,
      string? remarks)
  {
    ArgumentNullException.ThrowIfNull(propertyName);
    ArgumentNullException.ThrowIfNull(town);
    ArgumentNullException.ThrowIfNull(propertyType);
    ArgumentNullException.ThrowIfNull(classification);

    if (propertyName.Value.Length > NameMaxLength)
      throw new DomainException($"Property name cannot exceed {NameMaxLength} characters.");

    // an inactive master may stay on an old record, but cannot be newly chosen
    if (town.Id != TownId) town.EnsureActive();
    if (propertyType.Id != PropertyTypeId) propertyType.EnsureActive();
    if (classification.Id != PropertyClassificationId) classification.EnsureActive();

    PropertyName = propertyName;
    TownId = town.Id;
    PropertyTypeId = propertyType.Id;
    PropertyClassificationId = classification.Id;
    AddressLine = Guard.Text(addressLine, 300, "Address");
    KhasraSurveyNo = Guard.Text(khasraSurveyNo, 100, "Khasra / survey no.");
    Description = Guard.Text(description, 4000, "Description");
    Remarks = Guard.Text(remarks, 4000, "Remarks");
  }
}
