public sealed record GetAssetAttributeHistoryQueryResult(PaginatedResult<AssetAttributeHistoryDto> History);

public sealed record GetAssetAttributeHistoryQuery(Guid AssetId, PaginationRequest Pagination, string? AttributeCode = null)
  : IQuery<Result<GetAssetAttributeHistoryQueryResult>>;
