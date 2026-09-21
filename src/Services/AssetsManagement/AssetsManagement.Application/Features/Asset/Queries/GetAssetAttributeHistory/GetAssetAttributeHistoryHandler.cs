using Microsoft.EntityFrameworkCore;

public class GetAssetAttributeHistoryHandler(IApplicationDbContext context)
  : IQueryHandler<GetAssetAttributeHistoryQuery, Result<GetAssetAttributeHistoryQueryResult>>
{
  public async Task<Result<GetAssetAttributeHistoryQueryResult>> Handle(GetAssetAttributeHistoryQuery query, CancellationToken cancellationToken)
  {
    var assetId = AssetId.Of(query.AssetId);
    if (!await context.Assets.IgnoreQueryFilters().AnyAsync(a => a.Id == assetId, cancellationToken))
      throw new AssetNotFoundException($"Asset {query.AssetId} was not found.");

    var history = context.AssetAttributeHistories.AsNoTracking().Where(h => h.AssetId == assetId);

    if (!string.IsNullOrWhiteSpace(query.AttributeCode))
    {
      var code = AttributeCode.Of(query.AttributeCode).Value;
      history = history.Where(h => h.AttributeCode == code);
    }

    var total = await history.LongCountAsync(cancellationToken);
    var page = await history
      .OrderByDescending(h => h.ChangedAt)
      .Skip(query.Pagination.Pageindex * query.Pagination.PageSize)
      .Take(query.Pagination.PageSize)
      .ToListAsync(cancellationToken);

    return Result<GetAssetAttributeHistoryQueryResult>.Success(new GetAssetAttributeHistoryQueryResult(
      new PaginatedResult<AssetAttributeHistoryDto>(query.Pagination.Pageindex, query.Pagination.PageSize, total, page.Select(h => h.ToDto()).ToList())));
  }
}
