public sealed record GetAssetCategoriesQueryResult(IReadOnlyList<AssetCategoryDto> Categories);

/// ParentCategoryId = null with OnlyRoots = true returns the top level; omit both for the flat list of a class.
public sealed record GetAssetCategoriesQuery(Guid? AssetClassId, Guid? ParentCategoryId, bool OnlyRoots, bool OnlyLeaves, bool IncludeInactive, Guid? AssetTypeId = null)
  : IQuery<Result<GetAssetCategoriesQueryResult>>;
