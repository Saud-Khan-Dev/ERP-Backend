using Microsoft.EntityFrameworkCore;

public class GetAssetDisposalHandler(IApplicationDbContext context)
  : IQueryHandler<GetAssetDisposalQuery, Result<GetAssetDisposalQueryResult>>
{
  public async Task<Result<GetAssetDisposalQueryResult>> Handle(GetAssetDisposalQuery query, CancellationToken cancellationToken)
  {
    var assetId = AssetId.Of(query.AssetId);
    var disposal = await context.AssetDisposals.AsNoTracking()
      .FirstOrDefaultAsync(d => d.AssetId == assetId && (d.State == DisposalState.Proposed || d.State == DisposalState.Approved), cancellationToken)
      ?? throw new AssetDisposalNotFoundException("This asset has no disposal proposed or approved.");

    return Result<GetAssetDisposalQueryResult>.Success(new GetAssetDisposalQueryResult(disposal.ToDto()));
  }
}

public class GetAssetDisposalsHandler(IApplicationDbContext context)
  : IQueryHandler<GetAssetDisposalsQuery, Result<GetAssetDisposalsQueryResult>>
{
  public async Task<Result<GetAssetDisposalsQueryResult>> Handle(GetAssetDisposalsQuery query, CancellationToken cancellationToken)
  {
    var assetId = AssetId.Of(query.AssetId);
    var disposals = await context.AssetDisposals.AsNoTracking().Where(d => d.AssetId == assetId).ToListAsync(cancellationToken);
    var ordered = disposals.OrderByDescending(d => d.ProposedAt ?? d.ApprovedAt ?? DateTime.MinValue).Select(d => d.ToDto()).ToList();
    return Result<GetAssetDisposalsQueryResult>.Success(new GetAssetDisposalsQueryResult(ordered));
  }
}
