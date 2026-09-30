using Microsoft.EntityFrameworkCore;

public class GetAuctionsHandler(IApplicationDbContext context, MasterLookup masters, PropertyReadService read)
  : IQueryHandler<GetPropertyAuctionsQuery, Result<GetAuctionsQueryResult>>,
    IQueryHandler<GetAuctionQuery, Result<GetAuctionQueryResult>>
{
  public async Task<Result<GetAuctionsQueryResult>> Handle(GetPropertyAuctionsQuery query, CancellationToken cancellationToken)
  {
    var propertyId = await context.EnsurePropertyExistsAsync(query.PropertyId, cancellationToken);
    var rows = await context.Auctions.AsNoTracking().Include(a => a.Bids).Where(a => a.PropertyId == propertyId)
        .OrderByDescending(a => a.AuctionDate).ThenByDescending(a => a.CreatedAt).ToListAsync(cancellationToken);

    return Result<GetAuctionsQueryResult>.Success(new GetAuctionsQueryResult(await ToDtosAsync(rows, cancellationToken)));
  }

  public async Task<Result<GetAuctionQueryResult>> Handle(GetAuctionQuery query, CancellationToken cancellationToken)
  {
    var auction = await context.LoadAuctionAsync(query.Id, cancellationToken);
    return Result<GetAuctionQueryResult>.Success(new GetAuctionQueryResult((await ToDtosAsync(new[] { auction }, cancellationToken))[0]));
  }

  private async Task<List<AuctionDto>> ToDtosAsync(IReadOnlyCollection<PropertyAuction> rows, CancellationToken cancellationToken)
  {
    var refs = await masters.Refs()
        .Add<AuctionType>(rows.Select(a => a.AuctionTypeId))
        .Add<AuctionStatus>(rows.Select(a => a.AuctionStatusId))
        .LoadAsync(cancellationToken);
    var owners = await read.OwnerRefsAsync(
      rows.Select(a => a.SuccessfulBidderOwnerId).Concat(rows.SelectMany(a => a.Bids).Select(b => (OwnerId?)b.BidderOwnerId)), cancellationToken);

    return rows.Select(a => a.ToDto(refs, owners)).ToList();
  }
}
