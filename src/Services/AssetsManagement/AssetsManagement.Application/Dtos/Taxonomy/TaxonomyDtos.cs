public sealed record AssetClassDto(
  Guid Id,
  string Code,
  string Name,
  string? Description,
  int? DisplayOrder,
  bool IsActive);

public sealed record AssetTypeDto(
  Guid Id,
  Guid AssetClassId,
  string Code,
  string Name,
  string? Description,
  bool IsDepreciable,
  bool RequiresLocation,
  bool RequiresCustodian,
  int? DisplayOrder,
  bool IsActive);

public sealed record AssetCategoryDto(
  Guid Id,
  Guid AssetClassId,
  Guid? AssetTypeId,
  Guid? ParentCategoryId,
  string Code,
  string Name,
  string? Description,
  string Path,
  int Depth,
  bool IsLeaf,
  int? DisplayOrder,
  bool IsActive);

public static class TaxonomyMappings
{
  public static AssetClassDto ToDto(this AssetClass x) => new(
    x.Id.Value, x.Code.Value, x.Name.Value, x.Description, x.DisplayOrder, x.IsActive);

  public static AssetTypeDto ToDto(this AssetType x) => new(
    x.Id.Value, x.AssetClassId.Value, x.Code.Value, x.Name.Value, x.Description,
    x.IsDepreciable, x.RequiresLocation, x.RequiresCustodian, x.DisplayOrder, x.IsActive);

  public static AssetCategoryDto ToDto(this AssetCategory x) => new(
    x.Id.Value, x.AssetClassId.Value, x.AssetTypeId?.Value, x.ParentCategoryId?.Value,
    x.Code.Value, x.Name.Value, x.Description, x.Path, x.Depth, x.IsLeaf, x.DisplayOrder, x.IsActive);
}
