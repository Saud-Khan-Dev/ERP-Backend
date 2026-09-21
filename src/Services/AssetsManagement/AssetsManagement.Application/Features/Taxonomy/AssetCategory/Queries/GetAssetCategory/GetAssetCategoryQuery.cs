public sealed record GetAssetCategoryQueryResult(AssetCategoryDto Category, IReadOnlyList<AssetCategoryDto> Ancestors);

public sealed record GetAssetCategoryQuery(Guid Id) : IQuery<Result<GetAssetCategoryQueryResult>>;
