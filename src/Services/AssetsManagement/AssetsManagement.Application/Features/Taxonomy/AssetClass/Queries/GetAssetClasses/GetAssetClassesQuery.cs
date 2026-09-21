public sealed record GetAssetClassesQueryResult(IReadOnlyList<AssetClassDto> AssetClasses);

public sealed record GetAssetClassesQuery(bool IncludeInactive) : IQuery<Result<GetAssetClassesQueryResult>>;
