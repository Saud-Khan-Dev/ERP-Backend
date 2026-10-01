/// Level 3 of the taxonomy: one strict tree per class - Class > Type > Category > Sub-category, unlimited depth:
/// PHYSICAL > MOVABLE > IT Equipment > Computer > Laptop. Every category belongs to exactly one asset type, and a
/// sub-category always belongs to its parent's type, so the type decides which categories an asset can be put in.
/// Path is an ltree materialized path (physical.it_equipment.computer.laptop) rebuilt whenever the node moves.
public class AssetCategory : Aggregate<AssetCategoryId>
{
  public const char PathSeparator = '.';

  public AssetClassId AssetClassId { get; private set; } = default!;
  public AssetTypeId AssetTypeId { get; private set; } = default!;
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
      AssetTypeId assetTypeId,
      AssetCategory? parent,
      LookupCode code,
      Name name,
      string? description,
      int? displayOrder)
  {
    ArgumentNullException.ThrowIfNull(assetClassId);
    ArgumentNullException.ThrowIfNull(assetTypeId);
    ArgumentNullException.ThrowIfNull(code);
    ArgumentNullException.ThrowIfNull(name);

    if (parent is not null && parent.AssetClassId != assetClassId)
      throw new DomainException("A category must belong to the same asset class as its parent.");

    if (parent is not null && parent.AssetTypeId != assetTypeId)
      throw new DomainException("A sub-category belongs to the same asset type as its parent.");

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

  /// The type is fixed once the category exists (like its class): moving a branch to another type would silently
  /// change what its assets are. Create the category under the other type instead.
  public void Update(LookupCode code, Name name, string? description, int? displayOrder, bool isActive)
  {
    ArgumentNullException.ThrowIfNull(code);
    ArgumentNullException.ThrowIfNull(name);

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

      if (parent.AssetTypeId != AssetTypeId)
        throw new DomainException("A sub-category belongs to the same asset type as its parent.");

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
      throw new DomainException($"Category '{Name.Value}' has sub-categories. Choose one of its sub-categories for the asset.");

    if (!IsActive)
      throw new DomainException($"Category '{Name.Value}' is inactive.");
  }
}
