public sealed record GetAssetTypesQueryResult(IReadOnlyList<AssetTypeDto> AssetTypes);

public sealed record GetAssetTypesQuery(Guid? AssetClassId, bool IncludeInactive) : IQuery<Result<GetAssetTypesQueryResult>>;
