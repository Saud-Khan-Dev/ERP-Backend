using Microsoft.EntityFrameworkCore;

public class GetAssetAcquisitionHandler(IApplicationDbContext context)
  : IQueryHandler<GetAssetAcquisitionQuery, Result<GetAssetAcquisitionQueryResult>>
{
  public async Task<Result<GetAssetAcquisitionQueryResult>> Handle(GetAssetAcquisitionQuery query, CancellationToken cancellationToken)
  {
    var assetId = AssetId.Of(query.AssetId);
    var acquisition = await context.AssetAcquisitions.AsNoTracking().FirstOrDefaultAsync(a => a.AssetId == assetId, cancellationToken)
      ?? throw new AssetAcquisitionNotFoundException($"No acquisition record exists for asset {query.AssetId}.");

    return Result<GetAssetAcquisitionQueryResult>.Success(new GetAssetAcquisitionQueryResult(acquisition.ToDto()));
  }
}
