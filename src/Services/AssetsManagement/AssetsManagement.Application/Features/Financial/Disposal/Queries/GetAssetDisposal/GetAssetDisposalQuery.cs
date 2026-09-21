public sealed record GetAssetDisposalQueryResult(AssetDisposalDto Disposal);

public sealed record GetAssetDisposalQuery(Guid AssetId) : IQuery<Result<GetAssetDisposalQueryResult>>;
