using Microsoft.EntityFrameworkCore;

public class GetAssetClassHandler(IApplicationDbContext context)
  : IQueryHandler<GetAssetClassQuery, Result<GetAssetClassQueryResult>>
{
  public async Task<Result<GetAssetClassQueryResult>> Handle(GetAssetClassQuery query, CancellationToken cancellationToken)
  {
    var id = AssetClassId.Of(query.Id);
    var assetClass = await context.AssetClasses.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
      ?? throw new AssetClassNotFoundException($"Asset class {query.Id} was not found.");

    return Result<GetAssetClassQueryResult>.Success(new GetAssetClassQueryResult(assetClass.ToDto()));
  }
}
