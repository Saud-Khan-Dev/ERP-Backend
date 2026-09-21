/// Level 2 of the taxonomy. A class is subdivided into admin-defined types:
/// PHYSICAL -> MOVABLE / IMMOVABLE / CONSUMABLE / FLEET, FINANCIAL -> INVESTMENT / RECEIVABLE ...
public class AssetType : Aggregate<AssetTypeId>
{
  public AssetClassId AssetClassId { get; private set; } = default!;
  public LookupCode Code { get; private set; } = default!;
  public Name Name { get; private set; } = default!;
  public string? Description { get; private set; }
  public bool IsDepreciable { get; private set; }
  public bool RequiresLocation { get; private set; }
  public bool RequiresCustodian { get; private set; }
  public int? DisplayOrder { get; private set; }
  public bool IsActive { get; private set; }

  public static AssetType Create(
      AssetTypeId id,
      AssetClassId assetClassId,
      LookupCode code,
      Name name,
      string? description,
      bool isDepreciable,
      bool requiresLocation,
      bool requiresCustodian,
      int? displayOrder)
  {
    ArgumentNullException.ThrowIfNull(assetClassId);
    ArgumentNullException.ThrowIfNull(code);
    ArgumentNullException.ThrowIfNull(name);

    return new AssetType
    {
      Id = id,
      AssetClassId = assetClassId,
      Code = code,
      Name = name,
      Description = description,
      IsDepreciable = isDepreciable,
      RequiresLocation = requiresLocation,
      RequiresCustodian = requiresCustodian,
      DisplayOrder = displayOrder,
      IsActive = true
    };
  }

  public void Update(
      LookupCode code,
      Name name,
      string? description,
      bool isDepreciable,
      bool requiresLocation,
      bool requiresCustodian,
      int? displayOrder,
      bool isActive)
  {
    ArgumentNullException.ThrowIfNull(code);
    ArgumentNullException.ThrowIfNull(name);

    Code = code;
    Name = name;
    Description = description;
    IsDepreciable = isDepreciable;
    RequiresLocation = requiresLocation;
    RequiresCustodian = requiresCustodian;
    DisplayOrder = displayOrder;
    IsActive = isActive;
  }
}
