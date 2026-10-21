using System.Text.Json;

public class Asset : Aggregate<AssetId>
{
  /// Tags the system issues start with this; a tag typed by hand may not (it would collide with the series).
  public const string IssuedTagPrefix = "AST-";

  private Dictionary<string, JsonElement> _extraAttributes = new(StringComparer.Ordinal);

  public AssetCode AssetCode { get; private set; } = default!;
  public Name Name { get; private set; } = default!;
  public string? Description { get; private set; }
  public OwnershipType Ownership { get; private set; }

  public AssetClassId AssetClassId { get; private set; } = default!;
  public AssetTypeId AssetTypeId { get; private set; } = default!;
  public AssetCategoryId CategoryId { get; private set; } = default!;
  public AssetStatusId StatusId { get; private set; } = default!;

  // ---- current organizational state (HR links, with the names they had when set) ----
  public Guid? DepartmentId { get; private set; }
  public string? DepartmentName { get; private set; }
  public Guid? CustodianId { get; private set; }
  public string? CustodianName { get; private set; }
  public string? CustodianNumber { get; private set; }
  public LocationId? CurrentLocationId { get; private set; }

  // ---- identification ----
  public string? Barcode { get; private set; }

  /// Dynamic values: SOURCE OF TRUTH. Keys = attribute_definition.code, values are typed JSON. Values of fields that
  /// are no longer on the asset's form stay here (they are shown read-only) - a save never erases them.
  public IReadOnlyDictionary<string, JsonElement> ExtraAttributes => _extraAttributes;
  public DateTime? AttributesValidatedAt { get; private set; }

  // ---- system ----
  /// Kept for the database; every asset that is not deleted is active - statuses carry the meaning.
  public bool IsActive { get; private set; }
  public DateTime? DeletedAt { get; private set; }
  public string? DeletedBy { get; private set; }
  public string? DeletionReason { get; private set; }

  public bool IsDeleted => DeletedAt.HasValue;

  public CustodyRef Custody => new(DepartmentId, DepartmentName, CustodianId, CustodianName, CustodianNumber);

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
      CustodyRef custody,
      LocationId? currentLocationId,
      string? barcode)
  {
    ArgumentNullException.ThrowIfNull(assetCode);
    ArgumentNullException.ThrowIfNull(name);
    ArgumentNullException.ThrowIfNull(assetClass);
    ArgumentNullException.ThrowIfNull(assetType);
    ArgumentNullException.ThrowIfNull(category);
    ArgumentNullException.ThrowIfNull(status);
    ArgumentNullException.ThrowIfNull(custody);

    EnsureTaxonomyIsConsistent(assetClass, assetType, category);
    EnsureTypeRequirements(assetType, custody.CustodianId, currentLocationId);

    if (!status.IsActive)
      throw new DomainException($"The status '{status.Name.Value}' is switched off. Choose another status.");

    if (status.IsTerminal)
      throw new DomainException($"A new asset cannot start in the final status '{status.Name.Value}'.");

    var asset = new Asset
    {
      Id = id,
      AssetCode = assetCode,
      Name = name,
      Description = Clean(description),
      Ownership = ownership,
      AssetClassId = assetClass.Id,
      AssetTypeId = assetType.Id,
      CategoryId = category.Id,
      StatusId = status.Id,
      CurrentLocationId = currentLocationId,
      Barcode = Clean(barcode),
      IsActive = true
    };
    asset.SetCustody(custody);
    return asset;
  }

  /// Name, description, ownership and barcode. The tag is fixed once the asset is registered.
  public void UpdateDetails(Name name, string? description, OwnershipType ownership, AssetStatus currentStatus, string? barcode)
  {
    ArgumentNullException.ThrowIfNull(name);
    EnsureEditable(currentStatus);

    Name = name;
    Description = Clean(description);
    Ownership = ownership;
    Barcode = Clean(barcode);
  }

  /// Moves the asset to another category (of any type of the class). Values of fields the new form does not ask for
  /// are kept on the record.
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

  /// An ordinary status change (in service, in store, under repair). A final status is reached only by disposing of
  /// the asset, which also records how and at what book value.
  public void ChangeStatus(AssetStatus from, AssetStatus to)
  {
    ArgumentNullException.ThrowIfNull(from);
    ArgumentNullException.ThrowIfNull(to);
    EnsureCurrent(from);
    EnsureEditable(from);

    if (to.Id == from.Id)
      throw new DomainException($"The asset is already '{to.Name.Value}'.");

    if (!to.IsActive)
      throw new DomainException($"The status '{to.Name.Value}' is switched off. Choose another status.");

    if (to.IsTerminal)
      throw new DomainException($"'{to.Name.Value}' is a final status. Use Dispose to end the asset's life - it also records how it left and its book value.");

    StatusId = to.Id;
  }

  /// The disposal moves the asset to a final status; its record is closed from then on.
  public void Dispose(AssetStatus from, AssetStatus finalStatus)
  {
    ArgumentNullException.ThrowIfNull(from);
    ArgumentNullException.ThrowIfNull(finalStatus);
    EnsureCurrent(from);
    EnsureEditable(from);

    if (!finalStatus.IsTerminal)
      throw new DomainException($"'{finalStatus.Name.Value}' is not a final status. A disposal must end in a final status (e.g. Disposed).");

    if (!finalStatus.IsActive)
      throw new DomainException($"The status '{finalStatus.Name.Value}' is switched off. Choose another final status.");

    StatusId = finalStatus.Id;
  }

  /// A disposal recorded by mistake is cancelled: the asset goes back to the status it had before it.
  public void CancelDisposal(AssetStatus current, AssetStatus restoreTo)
  {
    ArgumentNullException.ThrowIfNull(current);
    ArgumentNullException.ThrowIfNull(restoreTo);
    EnsureCurrent(current);

    if (IsDeleted)
      throw new DomainException("This asset has been deleted.");

    if (!current.IsTerminal)
      throw new DomainException("The asset is not in a final status, so there is no disposal to cancel.");

    if (restoreTo.IsTerminal)
      throw new DomainException("The status to go back to must not be a final status.");

    StatusId = restoreTo.Id;
  }

  /// Applies the "to" side of a transfer or loan.
  public void AssignTo(AssetStatus currentStatus, AssetType assetType, CustodyRef custody, LocationId? locationId)
  {
    ArgumentNullException.ThrowIfNull(currentStatus);
    ArgumentNullException.ThrowIfNull(assetType);
    ArgumentNullException.ThrowIfNull(custody);
    EnsureCurrent(currentStatus);
    EnsureEditable(currentStatus);

    if (!currentStatus.AllowsAssignment)
      throw new DomainException($"An asset that is '{currentStatus.Name.Value}' cannot be moved or issued. Change its status first.");

    EnsureTypeRequirements(assetType, custody.CustodianId, locationId);

    SetCustody(custody);
    CurrentLocationId = locationId;
  }

  /// A loan comes back to where it was issued from. Always possible, whatever the asset's status is now (an asset
  /// can come back from a loan broken, already marked "under repair").
  public void ReturnFromLoan(AssetStatus currentStatus, CustodyRef custody, LocationId? locationId)
  {
    ArgumentNullException.ThrowIfNull(currentStatus);
    ArgumentNullException.ThrowIfNull(custody);
    EnsureCurrent(currentStatus);
    EnsureEditable(currentStatus);

    SetCustody(custody);
    CurrentLocationId = locationId;
  }

  /// Replaces the validated dynamic attribute bag. Only AttributeValueValidator output should be passed here.
  public void SetExtraAttributes(IReadOnlyDictionary<string, JsonElement> validatedAttributes, DateTime validatedAt)
  {
    ArgumentNullException.ThrowIfNull(validatedAttributes);

    _extraAttributes = new Dictionary<string, JsonElement>(validatedAttributes, StringComparer.Ordinal);
    AttributesValidatedAt = validatedAt;
  }

  /// Takes the asset out of the register (it stays in the database). Why, and by whom, is kept with it.
  public void SoftDelete(string? deletedBy, string reason, DateTime deletedAt)
  {
    if (IsDeleted)
      throw new DomainException("This asset has already been deleted.");

    if (string.IsNullOrWhiteSpace(reason))
      throw new DomainException("Say why the asset is deleted.");

    DeletedAt = deletedAt;
    DeletedBy = deletedBy;
    DeletionReason = reason.Trim();
    IsActive = false;
  }

  public void EnsureEditable(AssetStatus currentStatus)
  {
    ArgumentNullException.ThrowIfNull(currentStatus);

    if (IsDeleted)
      throw new DomainException("This asset has been deleted.");

    if (currentStatus.Id == StatusId && currentStatus.IsTerminal)
      throw new DomainException($"This asset is '{currentStatus.Name.Value}' - its record is closed and cannot change any more.");
  }

  private void EnsureCurrent(AssetStatus status)
  {
    if (status.Id != StatusId)
      throw new DomainException("The asset's status changed in the meantime. Reload it and try again.");
  }

  private void SetCustody(CustodyRef custody)
  {
    DepartmentId = custody.DepartmentId;
    DepartmentName = custody.DepartmentId is null ? null : Clean(custody.DepartmentName);
    CustodianId = custody.CustodianId;
    CustodianName = custody.CustodianId is null ? null : Clean(custody.CustodianName);
    CustodianNumber = custody.CustodianId is null ? null : Clean(custody.CustodianNumber);
  }

  private static void EnsureTaxonomyIsConsistent(AssetClass assetClass, AssetType assetType, AssetCategory category)
  {
    if (!assetClass.IsActive)
      throw new DomainException($"The asset class '{assetClass.Name.Value}' is switched off.");

    if (!assetType.IsActive)
      throw new DomainException($"The asset type '{assetType.Name.Value}' is switched off.");

    if (assetType.AssetClassId != assetClass.Id)
      throw new DomainException($"The asset type '{assetType.Name.Value}' does not belong to the class '{assetClass.Name.Value}'.");

    if (category.AssetClassId != assetClass.Id)
      throw new DomainException($"The category '{category.Name.Value}' does not belong to the class '{assetClass.Name.Value}'.");

    if (category.AssetTypeId != assetType.Id)
      throw new DomainException($"The category '{category.Name.Value}' belongs to another asset type than '{assetType.Name.Value}'.");

    category.EnsureAcceptsAssets();
  }

  private static void EnsureTypeRequirements(AssetType assetType, Guid? custodianId, LocationId? locationId)
  {
    if (assetType.RequiresLocation && locationId is null)
      throw new DomainException($"Assets of type '{assetType.Name.Value}' must have a location.");

    if (assetType.RequiresCustodian && (custodianId is null || custodianId == Guid.Empty))
      throw new DomainException($"Assets of type '{assetType.Name.Value}' must have a custodian.");
  }

  private static string? Clean(string? value) =>
      string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
