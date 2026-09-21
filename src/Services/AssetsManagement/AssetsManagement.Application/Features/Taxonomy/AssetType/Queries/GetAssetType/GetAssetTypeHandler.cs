using Microsoft.EntityFrameworkCore;

public class GetAssetTypeHandler(IApplicationDbContext context)
  : IQueryHandler<GetAssetTypeQuery, Result<GetAssetTypeQueryResult>>
{
  public async Task<Result<GetAssetTypeQueryResult>> Handle(GetAssetTypeQuery query, CancellationToken cancellationToken)
  {
    var id = AssetTypeId.Of(query.Id);
    var assetType = await context.AssetTypes.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
      ?? throw new AssetTypeNotFoundException($"Asset type {query.Id} was not found.");

    return Result<GetAssetTypeQueryResult>.Success(new GetAssetTypeQueryResult(assetType.ToDto()));
  }
}
