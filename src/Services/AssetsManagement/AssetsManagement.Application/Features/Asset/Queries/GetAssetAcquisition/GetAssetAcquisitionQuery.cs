public sealed record GetAssetAcquisitionQueryResult(AssetAcquisitionDto Acquisition);

public sealed record GetAssetAcquisitionQuery(Guid AssetId) : IQuery<Result<GetAssetAcquisitionQueryResult>>;
