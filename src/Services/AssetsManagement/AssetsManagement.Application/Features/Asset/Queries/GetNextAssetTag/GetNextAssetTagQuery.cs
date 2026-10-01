public sealed record GetNextAssetTagQueryResult(string AssetCode);

/// The tag the next registration will get when it leaves the tag to the system. A preview, not a reservation:
/// two clerks registering at the same moment get consecutive tags when they save.
public sealed record GetNextAssetTagQuery : IQuery<Result<GetNextAssetTagQueryResult>>;

public class GetNextAssetTagHandler(IApplicationDbContext context)
  : IQueryHandler<GetNextAssetTagQuery, Result<GetNextAssetTagQueryResult>>
{
  public async Task<Result<GetNextAssetTagQueryResult>> Handle(GetNextAssetTagQuery query, CancellationToken cancellationToken)
  {
    var code = await AssetCodeIssuer.NextAsync(context, cancellationToken);
    return Result<GetNextAssetTagQueryResult>.Success(new GetNextAssetTagQueryResult(code.Value));
  }
}
