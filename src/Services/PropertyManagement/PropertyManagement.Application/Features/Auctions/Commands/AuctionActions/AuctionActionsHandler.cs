public class AuctionActionsHandler(IApplicationDbContext context, MasterLookup masters)
  : ICommandHandler<UpdateAuctionCommand, Result<AuctionActionResult>>,
    ICommandHandler<ChangeAuctionStatusCommand, Result<AuctionActionResult>>,
    ICommandHandler<RecordBidCommand, Result<RecordBidCommandResult>>,
    ICommandHandler<AwardAuctionCommand, Result<AuctionActionResult>>,
    ICommandHandler<CancelAuctionCommand, Result<AuctionActionResult>>
{
  public async Task<Result<AuctionActionResult>> Handle(UpdateAuctionCommand command, CancellationToken cancellationToken)
  {
    var auction = await context.LoadAuctionAsync(command.Id, cancellationToken);
    auction.Update(await Current(auction, cancellationToken), await command.Auction.ToDetailsAsync(masters, cancellationToken));
    return await SaveAsync(auction, cancellationToken);
  }

  public async Task<Result<AuctionActionResult>> Handle(ChangeAuctionStatusCommand command, CancellationToken cancellationToken)
  {
    var auction = await context.LoadAuctionAsync(command.Id, cancellationToken);
    auction.ChangeStatus(await Current(auction, cancellationToken), await masters.GetAsync<AuctionStatus>(command.AuctionStatusId, cancellationToken));
    return await SaveAsync(auction, cancellationToken);
  }

  public async Task<Result<RecordBidCommandResult>> Handle(RecordBidCommand command, CancellationToken cancellationToken)
  {
    var auction = await context.LoadAuctionAsync(command.Id, cancellationToken);
    var bidder = await context.LoadOwnerAsync(command.BidderOwnerId, cancellationToken);

    var bid = auction.AddBid(await Current(auction, cancellationToken), bidder, command.BidAmount, command.EarnestMoney, command.Remarks);

    await context.SaveChangesAsync(cancellationToken);
    return Result<RecordBidCommandResult>.Success(new RecordBidCommandResult(bid.Id.Value, bid.BidRank));
  }

  public async Task<Result<AuctionActionResult>> Handle(AwardAuctionCommand command, CancellationToken cancellationToken)
  {
    var auction = await context.LoadAuctionAsync(command.Id, cancellationToken);
    var bidder = command.SuccessfulBidderOwnerId is { } bidderId ? await context.LoadOwnerAsync(bidderId, cancellationToken) : null;

    auction.Award(
      await Current(auction, cancellationToken),
      await masters.GetByCodeAsync<AuctionStatus>(SystemMasterCodes.Awarded, cancellationToken),
      command.WinningBidId is { } bidId ? AuctionBidId.Of(bidId) : null,
      bidder, command.WinningBidAmount, command.AwardDate, command.AwardReferenceNo);

    return await SaveAsync(auction, cancellationToken);
  }

  public async Task<Result<AuctionActionResult>> Handle(CancelAuctionCommand command, CancellationToken cancellationToken)
  {
    var auction = await context.LoadAuctionAsync(command.Id, cancellationToken);
    auction.Cancel(await Current(auction, cancellationToken),
      await masters.GetByCodeAsync<AuctionStatus>(SystemMasterCodes.Cancelled, cancellationToken), command.Remarks);
    return await SaveAsync(auction, cancellationToken);
  }

  private Task<AuctionStatus> Current(PropertyAuction auction, CancellationToken cancellationToken) =>
      masters.GetAsync<AuctionStatus>(auction.AuctionStatusId.Value, cancellationToken);

  private async Task<Result<AuctionActionResult>> SaveAsync(PropertyAuction auction, CancellationToken cancellationToken)
  {
    await context.SaveChangesAsync(cancellationToken);
    var refs = await masters.Refs().Add<AuctionStatus>(auction.AuctionStatusId).LoadAsync(cancellationToken);
    return Result<AuctionActionResult>.Success(new AuctionActionResult(auction.Id.Value, refs[auction.AuctionStatusId]));
  }
}
