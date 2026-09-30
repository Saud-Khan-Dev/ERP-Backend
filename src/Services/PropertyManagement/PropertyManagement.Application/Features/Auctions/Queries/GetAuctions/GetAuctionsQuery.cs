public sealed record GetAuctionsQueryResult(IReadOnlyList<AuctionDto> Auctions);
public sealed record GetAuctionQueryResult(AuctionDto Auction);

public sealed record GetPropertyAuctionsQuery(Guid PropertyId) : IQuery<Result<GetAuctionsQueryResult>>;

public sealed record GetAuctionQuery(Guid Id) : IQuery<Result<GetAuctionQueryResult>>;
