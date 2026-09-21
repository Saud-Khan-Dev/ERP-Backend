using Microsoft.EntityFrameworkCore;

public class GetAssetDisposalHandler(IApplicationDbContext context)
  : IQueryHandler<GetAssetDisposalQuery, Result<GetAssetDisposalQueryResult>>
{
  public async Task<Result<GetAssetDisposalQueryResult>> Handle(GetAssetDisposalQuery query, CancellationToken cancellationToken)
  {
    var assetId = AssetId.Of(query.AssetId);
    var disposal = await context.AssetDisposals.AsNoTracking().FirstOrDefaultAsync(d => d.AssetId == assetId, cancellationToken)
      ?? throw new AssetDisposalNotFoundException($"No disposal record exists for asset {query.AssetId}.");

    return Result<GetAssetDisposalQueryResult>.Success(new GetAssetDisposalQueryResult(disposal.ToDto()));
  }
}
