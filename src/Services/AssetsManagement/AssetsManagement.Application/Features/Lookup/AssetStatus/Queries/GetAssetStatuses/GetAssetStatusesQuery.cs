public sealed record GetAssetStatusesQueryResult(IReadOnlyList<AssetStatusDto> Statuses);

public sealed record GetAssetStatusesQuery(bool IncludeInactive) : IQuery<Result<GetAssetStatusesQueryResult>>;
