public sealed record GetAssetClassQueryResult(AssetClassDto AssetClass);

public sealed record GetAssetClassQuery(Guid Id) : IQuery<Result<GetAssetClassQueryResult>>;
