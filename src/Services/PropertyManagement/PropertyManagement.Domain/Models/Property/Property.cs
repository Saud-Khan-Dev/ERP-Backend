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
  public bool IsActive { get; private set; }

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

  public void UpdateDetails(
      Name propertyName,
      Town town,
      PropertyType propertyType,
      PropertyClassification classification,
      string? addressLine,
      string? khasraSurveyNo,
      string? description,
      string? remarks)
  {
    EnsureActive();
    Apply(propertyName, town, propertyType, classification, addressLine, khasraSurveyNo, description, remarks);
  }

  /// Opening row of the status history, written when the property is registered.
  public PropertyStatusHistory OpenStatusHistory(DateOnly effectiveFrom, string? reason) =>
      PropertyStatusHistory.Open(PropertyStatusHistoryId.New(), Id, PropertyStatusId, effectiveFrom, reason, referenceNo: null);

  /// Moves the property to a new status: closes the current history row and returns the new one
  /// (schema guide, rule 2 — history is never overwritten).
  public PropertyStatusHistory ChangeStatus(
      PropertyStatus newStatus,
      PropertyStatusHistory? currentHistory,
      DateOnly effectiveFrom,
      string? reason,
      string? referenceNo)
  {
    ArgumentNullException.ThrowIfNull(newStatus);
    EnsureActive();
    newStatus.EnsureActive();

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

  /// Nothing is hard-deleted (schema guide): a retired property is deactivated.
  public void Deactivate()
  {
    if (!IsActive)
      throw new DomainException($"Property {PropertyCode.Value} is already inactive.");

    IsActive = false;
  }

  public void Activate() => IsActive = true;

  /// Child records (measurements, ownership, documents ...) may only be added to an active property.
  public void EnsureActive()
  {
    if (!IsActive)
      throw new DomainException($"Property {PropertyCode.Value} is inactive.");
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
