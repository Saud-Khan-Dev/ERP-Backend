public sealed record GetNextAssetTagQueryResult(string AssetCode);

/// The tag the next registration will get when it leaves the tag to the system. A preview, not a reservation: when
/// someone else registers first, the registration gets the one after.
public sealed record GetNextAssetTagQuery : IQuery<Result<GetNextAssetTagQueryResult>>;

public class GetNextAssetTagHandler(IAssetDataQueries queries)
  : IQueryHandler<GetNextAssetTagQuery, Result<GetNextAssetTagQueryResult>>
{
  public async Task<Result<GetNextAssetTagQueryResult>> Handle(GetNextAssetTagQuery query, CancellationToken cancellationToken)
  {
    var code = await AssetCodeIssuer.PeekAsync(queries, cancellationToken);
    return Result<GetNextAssetTagQueryResult>.Success(new GetNextAssetTagQueryResult(code.Value));
  }
}
