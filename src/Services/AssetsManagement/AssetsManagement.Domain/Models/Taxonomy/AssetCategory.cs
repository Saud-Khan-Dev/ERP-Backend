/// Level 3 of the taxonomy. Hierarchical, unlimited depth, scoped to a class:
/// PHYSICAL > IT Equipment > Computer > Laptop.
/// Path is an ltree materialized path (physical.it_equipment.computer.laptop) rebuilt whenever the node moves.
public class AssetCategory : Aggregate<AssetCategoryId>
{
  public const char PathSeparator = '.';

  public AssetClassId AssetClassId { get; private set; } = default!;
  public AssetTypeId? AssetTypeId { get; private set; }
  public AssetCategoryId? ParentCategoryId { get; private set; }
  public LookupCode Code { get; private set; } = default!;
  public Name Name { get; private set; } = default!;
  public string? Description { get; private set; }
  public string Path { get; private set; } = default!;
  public int Depth { get; private set; }
  public bool IsLeaf { get; private set; }
  public int? DisplayOrder { get; private set; }
  public bool IsActive { get; private set; }

  public static AssetCategory Create(
      AssetCategoryId id,
      AssetClassId assetClassId,
      AssetTypeId? assetTypeId,
      AssetCategory? parent,
      LookupCode code,
      Name name,
      string? description,
      int? displayOrder)
  {
    ArgumentNullException.ThrowIfNull(assetClassId);
    ArgumentNullException.ThrowIfNull(code);
    ArgumentNullException.ThrowIfNull(name);

    if (parent is not null && parent.AssetClassId != assetClassId)
      throw new DomainException("A category must belong to the same asset class as its parent.");

    var category = new AssetCategory
    {
      Id = id,
      AssetClassId = assetClassId,
      AssetTypeId = assetTypeId,
      Code = code,
      Name = name,
      Description = description,
      DisplayOrder = displayOrder,
      IsLeaf = true,
      IsActive = true
    };

    category.Rebase(parent);
    parent?.MarkAsBranch();

    return category;
  }

  public void Update(AssetTypeId? assetTypeId, LookupCode code, Name name, string? description, int? displayOrder, bool isActive)
  {
    ArgumentNullException.ThrowIfNull(code);
    ArgumentNullException.ThrowIfNull(name);

    AssetTypeId = assetTypeId;
    Code = code;
    Name = name;
    Description = description;
    DisplayOrder = displayOrder;
    IsActive = isActive;
  }

  /// Re-parents the node and recomputes its path/depth. Descendants must be rebased afterwards (root -> leaf order).
  public void Rebase(AssetCategory? parent)
  {
    if (parent is not null)
    {
      if (parent.Id == Id)
        throw new DomainException("A category cannot be its own parent.");

      if (parent.AssetClassId != AssetClassId)
        throw new DomainException("A category must belong to the same asset class as its parent.");

      if (Path is not null && IsAncestorOf(parent))
        throw new DomainException("Moving a category under one of its own descendants would create a cycle.");
    }

    ParentCategoryId = parent?.Id;
    Depth = parent is null ? 0 : parent.Depth + 1;
    Path = parent is null
        ? Code.ToPathLabel()
        : string.Concat(parent.Path, PathSeparator, Code.ToPathLabel());
  }

  public bool IsAncestorOf(AssetCategory other) =>
      other.Path.StartsWith(Path + PathSeparator, StringComparison.Ordinal);

  public void MarkAsBranch() => IsLeaf = false;

  public void MarkAsLeaf() => IsLeaf = true;

  /// Assets may only be attached to leaf categories.
  public void EnsureAcceptsAssets()
  {
    if (!IsLeaf)
      throw new DomainException($"Category '{Name.Value}' is not a leaf category. Assets can only be attached to leaf categories.");

    if (!IsActive)
      throw new DomainException($"Category '{Name.Value}' is inactive.");
  }
}
