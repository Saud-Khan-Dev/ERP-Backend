public sealed record GetAssetValuationsQueryResult(IReadOnlyList<AssetValuationDto> Valuations);

public sealed record GetAssetValuationsQuery(Guid AssetId) : IQuery<Result<GetAssetValuationsQueryResult>>;
