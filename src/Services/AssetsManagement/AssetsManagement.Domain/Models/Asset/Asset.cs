using System.Text.Json;

public class Asset : Aggregate<AssetId>
{
  private Dictionary<string, JsonElement> _extraAttributes = new(StringComparer.Ordinal);

  public AssetCode AssetCode { get; private set; } = default!;
  public Name Name { get; private set; } = default!;
  public string? Description { get; private set; }
  public OwnershipType Ownership { get; private set; }

  public AssetClassId AssetClassId { get; private set; } = default!;
  public AssetTypeId AssetTypeId { get; private set; } = default!;
  public AssetCategoryId CategoryId { get; private set; } = default!;
  public AssetStatusId StatusId { get; private set; } = default!;

  // ---- current organizational state ----
  public Guid? DepartmentId { get; private set; }
  public Guid? CustodianId { get; private set; }
  public LocationId? CurrentLocationId { get; private set; }

  // ---- identification ----
  public string? Barcode { get; private set; }

  /// Dynamic values: SOURCE OF TRUTH. Keys = attribute_definition.code, values are typed JSON.
  public IReadOnlyDictionary<string, JsonElement> ExtraAttributes => _extraAttributes;
  public DateTime? AttributesValidatedAt { get; private set; }

  // ---- system ----
  public bool IsActive { get; private set; }
  public DateTime? DeletedAt { get; private set; }
  public string? DeletedBy { get; private set; }

  public bool IsDeleted => DeletedAt.HasValue;

  public static Asset Create(
      AssetId id,
      AssetCode assetCode,
      Name name,
      string? description,
      OwnershipType ownership,
      AssetClass assetClass,
      AssetType assetType,
      AssetCategory category,
      AssetStatus status,
      Guid? departmentId,
      Guid? custodianId,
      LocationId? currentLocationId,
      string? barcode)
  {
    ArgumentNullException.ThrowIfNull(assetCode);
    ArgumentNullException.ThrowIfNull(name);
    ArgumentNullException.ThrowIfNull(assetClass);
    ArgumentNullException.ThrowIfNull(assetType);
    ArgumentNullException.ThrowIfNull(category);
    ArgumentNullException.ThrowIfNull(status);

    EnsureTaxonomyIsConsistent(assetClass, assetType, category);
    EnsureTypeRequirements(assetType, custodianId, currentLocationId);

    if (!status.IsActive)
      throw new DomainException($"Status '{status.Name.Value}' is inactive.");

    if (status.IsTerminal)
      throw new DomainException($"A new asset cannot start in the final status '{status.Name.Value}'.");

    return new Asset
    {
      Id = id,
      AssetCode = assetCode,
      Name = name,
      Description = description,
      Ownership = ownership,
      AssetClassId = assetClass.Id,
      AssetTypeId = assetType.Id,
      CategoryId = category.Id,
      StatusId = status.Id,
      DepartmentId = departmentId,
      CustodianId = custodianId,
      CurrentLocationId = currentLocationId,
      Barcode = NormalizeIdentifier(barcode),
      IsActive = true
    };
  }

  public void UpdateDetails(
      AssetCode assetCode,
      Name name,
      string? description,
      OwnershipType ownership,
      AssetStatus currentStatus,
      string? barcode,
      bool isActive)
  {
    ArgumentNullException.ThrowIfNull(assetCode);
    ArgumentNullException.ThrowIfNull(name);
    EnsureEditable(currentStatus);

    AssetCode = assetCode;
    Name = name;
    Description = description;
    Ownership = ownership;
    Barcode = NormalizeIdentifier(barcode);
    IsActive = isActive;
  }

  /// Moves the asset to another leaf category of the same class/type. Attribute values must be re-validated afterwards.
  public void Reclassify(AssetClass assetClass, AssetType assetType, AssetCategory category, AssetStatus currentStatus)
  {
    ArgumentNullException.ThrowIfNull(assetClass);
    ArgumentNullException.ThrowIfNull(assetType);
    ArgumentNullException.ThrowIfNull(category);
    EnsureEditable(currentStatus);
    EnsureTaxonomyIsConsistent(assetClass, assetType, category);
    EnsureTypeRequirements(assetType, CustodianId, CurrentLocationId);

    AssetClassId = assetClass.Id;
    AssetTypeId = assetType.Id;
    CategoryId = category.Id;
  }

  public void ChangeStatus(AssetStatus from, AssetStatus to, bool hasActiveDepreciationSchedule)
  {
    ArgumentNullException.ThrowIfNull(from);
    ArgumentNullException.ThrowIfNull(to);

    if (from.Id != StatusId)
      throw new DomainException("The supplied current status does not match the asset.");

    if (from.IsTerminal)
      throw new DomainException($"Asset is in terminal status '{from.Name.Value}' and cannot change status.");

    if (!to.IsActive)
      throw new DomainException($"Status '{to.Name.Value}' is inactive.");

    if (to.IsTerminal && hasActiveDepreciationSchedule)
      throw new DomainException("Close or deactivate the active depreciation schedule before moving the asset to a terminal status.");

    StatusId = to.Id;
  }

  /// Applies the "to" side of an AssetAssignment (transfer / issue / return).
  public void AssignTo(AssetStatus currentStatus, AssetType assetType, Guid? departmentId, Guid? custodianId, LocationId? locationId)
  {
    ArgumentNullException.ThrowIfNull(currentStatus);
    ArgumentNullException.ThrowIfNull(assetType);
    EnsureEditable(currentStatus);

    if (!currentStatus.AllowsAssignment)
      throw new DomainException($"Assets in status '{currentStatus.Name.Value}' cannot be assigned or transferred.");

    EnsureTypeRequirements(assetType, custodianId, locationId);

    DepartmentId = departmentId;
    CustodianId = custodianId;
    CurrentLocationId = locationId;
  }

  /// Replaces the validated dynamic attribute bag. Only AttributeValueValidator output should be passed here.
  public void SetExtraAttributes(IReadOnlyDictionary<string, JsonElement> validatedAttributes, DateTime validatedAt)
  {
    ArgumentNullException.ThrowIfNull(validatedAttributes);

    _extraAttributes = new Dictionary<string, JsonElement>(validatedAttributes, StringComparer.Ordinal);
    AttributesValidatedAt = validatedAt;
  }

  public void SoftDelete(string? deletedBy, DateTime deletedAt)
  {
    if (IsDeleted)
      throw new DomainException("Asset is already deleted.");

    DeletedAt = deletedAt;
    DeletedBy = deletedBy;
    IsActive = false;
  }

  public void EnsureEditable(AssetStatus currentStatus)
  {
    ArgumentNullException.ThrowIfNull(currentStatus);

    if (IsDeleted)
      throw new DomainException("Asset has been deleted.");

    if (currentStatus.Id == StatusId && currentStatus.IsTerminal)
      throw new DomainException($"Asset is in terminal status '{currentStatus.Name.Value}' and cannot be edited.");
  }

  private static void EnsureTaxonomyIsConsistent(AssetClass assetClass, AssetType assetType, AssetCategory category)
  {
    if (!assetClass.IsActive)
      throw new DomainException($"Asset class '{assetClass.Name.Value}' is inactive.");

    if (!assetType.IsActive)
      throw new DomainException($"Asset type '{assetType.Name.Value}' is inactive.");

    if (assetType.AssetClassId != assetClass.Id)
      throw new DomainException($"Asset type '{assetType.Name.Value}' does not belong to class '{assetClass.Name.Value}'.");

    if (category.AssetClassId != assetClass.Id)
      throw new DomainException($"Category '{category.Name.Value}' does not belong to class '{assetClass.Name.Value}'.");

    if (category.AssetTypeId != assetType.Id)
      throw new DomainException($"Category '{category.Name.Value}' belongs to a different asset type than '{assetType.Name.Value}'.");

    category.EnsureAcceptsAssets();
  }

  private static void EnsureTypeRequirements(AssetType assetType, Guid? custodianId, LocationId? locationId)
  {
    if (assetType.RequiresLocation && locationId is null)
      throw new DomainException($"Assets of type '{assetType.Name.Value}' require a location.");

    if (assetType.RequiresCustodian && (custodianId is null || custodianId == Guid.Empty))
      throw new DomainException($"Assets of type '{assetType.Name.Value}' require a custodian.");
  }

  private static string? NormalizeIdentifier(string? value) =>
      string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
