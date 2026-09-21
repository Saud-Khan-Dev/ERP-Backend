using Microsoft.EntityFrameworkCore;

public class GetAssetValuationsHandler(IApplicationDbContext context)
  : IQueryHandler<GetAssetValuationsQuery, Result<GetAssetValuationsQueryResult>>
{
  public async Task<Result<GetAssetValuationsQueryResult>> Handle(GetAssetValuationsQuery query, CancellationToken cancellationToken)
  {
    var assetId = AssetId.Of(query.AssetId);
    if (!await context.Assets.IgnoreQueryFilters().AnyAsync(a => a.Id == assetId, cancellationToken))
      throw new AssetNotFoundException($"Asset {query.AssetId} was not found.");

    var data = await context.AssetValuations.AsNoTracking()
      .Where(v => v.AssetId == assetId)
      .OrderByDescending(v => v.ValuationDate)
      .ToListAsync(cancellationToken);

    return Result<GetAssetValuationsQueryResult>.Success(new GetAssetValuationsQueryResult(data.Select(v => v.ToDto()).ToList()));
  }
}
