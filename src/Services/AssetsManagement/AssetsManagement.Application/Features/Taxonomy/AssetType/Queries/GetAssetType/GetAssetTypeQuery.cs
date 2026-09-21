public sealed record GetAssetTypeQueryResult(AssetTypeDto AssetType);

public sealed record GetAssetTypeQuery(Guid Id) : IQuery<Result<GetAssetTypeQueryResult>>;
