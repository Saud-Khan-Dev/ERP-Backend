using Microsoft.EntityFrameworkCore;

public class AuctionActionsHandler(IApplicationDbContext context, MasterLookup masters, CodeGenerator codes, IAuditTrail audit)
  : ICommandHandler<UpdateAuctionCommand, Result<AuctionActionResult>>,
    ICommandHandler<ChangeAuctionStatusCommand, Result<AuctionActionResult>>,
    ICommandHandler<RecordBidCommand, Result<RecordBidCommandResult>>,
    ICommandHandler<AwardAuctionCommand, Result<AuctionActionResult>>,
    ICommandHandler<CancelAuctionCommand, Result<AuctionActionResult>>,
    ICommandHandler<ReAuctionCommand, Result<ReAuctionCommandResult>>
{
  public async Task<Result<AuctionActionResult>> Handle(UpdateAuctionCommand command, CancellationToken cancellationToken)
  {
    var auction = await context.LoadAuctionAsync(command.Id, cancellationToken);
    await context.EnsurePropertyActiveAsync(auction.PropertyId, cancellationToken);
    auction.Update(await Current(auction, cancellationToken), await command.Auction.ToDetailsAsync(masters, cancellationToken));
    return await SaveAsync(auction, cancellationToken);
  }

  public async Task<Result<AuctionActionResult>> Handle(ChangeAuctionStatusCommand command, CancellationToken cancellationToken)
  {
    var auction = await context.LoadAuctionAsync(command.Id, cancellationToken);
    await context.EnsurePropertyActiveAsync(auction.PropertyId, cancellationToken);
    auction.ChangeStatus(await Current(auction, cancellationToken), await masters.GetAsync<AuctionStatus>(command.AuctionStatusId, cancellationToken));
    return await SaveAsync(auction, cancellationToken);
  }

  public async Task<Result<RecordBidCommandResult>> Handle(RecordBidCommand command, CancellationToken cancellationToken)
  {
    var auction = await context.LoadAuctionAsync(command.Id, cancellationToken);
    await context.EnsurePropertyActiveAsync(auction.PropertyId, cancellationToken);
    var bidder = await context.LoadOwnerAsync(command.BidderOwnerId, cancellationToken);

    var bid = auction.AddBid(await Current(auction, cancellationToken), bidder, command.BidAmount, command.EarnestMoney, command.Remarks);

    await context.SaveChangesAsync(cancellationToken);
    return Result<RecordBidCommandResult>.Success(new RecordBidCommandResult(bid.Id.Value, bid.BidRank));
  }

  public async Task<Result<AuctionActionResult>> Handle(AwardAuctionCommand command, CancellationToken cancellationToken)
  {
    var auction = await context.LoadAuctionAsync(command.Id, cancellationToken);
    await context.EnsurePropertyActiveAsync(auction.PropertyId, cancellationToken);
    var bidder = command.SuccessfulBidderOwnerId is { } bidderId ? await context.LoadOwnerAsync(bidderId, cancellationToken) : null;

    auction.Award(
      await Current(auction, cancellationToken),
      await masters.GetByCodeAsync<AuctionStatus>(SystemMasterCodes.Awarded, cancellationToken),
      command.WinningBidId is { } bidId ? AuctionBidId.Of(bidId) : null,
      bidder, command.WinningBidAmount, command.AwardDate, command.AwardReferenceNo, PropertyWriteGuard.Today());
    audit.Record(PropertyAuditActions.Awarded, auction, string.Join(" · ", new[] { bidder is null ? null : $"to {bidder.OwnerName.Value} ({bidder.OwnerCode.Value})", command.WinningBidAmount is { } amount ? $"Rs {amount:N0}" : null, command.AwardReferenceNo }.Where(x => x is not null)));

    return await SaveAsync(auction, cancellationToken);
  }

  public async Task<Result<ReAuctionCommandResult>> Handle(ReAuctionCommand command, CancellationToken cancellationToken)
  {
    var auction = await context.LoadAuctionAsync(command.Id, cancellationToken);
    var property = await context.EnsurePropertyActiveAsync(auction.PropertyId, cancellationToken);
    var existing = await context.Auctions.AsNoTracking().FirstOrDefaultAsync(a => a.PreviousAuctionId == auction.Id, cancellationToken);

    var reAuction = auction.ReAuction(
      await Current(auction, cancellationToken),
      property,
      AuctionId.New(),
      await codes.NextAsync(CodeSequenceKeys.Auction, cancellationToken),
      await masters.GetByCodeAsync<AuctionStatus>(SystemMasterCodes.Planned, cancellationToken),
      await masters.GetByCodeAsync<AuctionType>(SystemMasterCodes.AuctionReAuction, cancellationToken),
      command.AuctionDate, command.BaseReservePrice, command.AuctionCommitteeRef, existing);

    await context.Auctions.AddAsync(reAuction, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    return Result<ReAuctionCommandResult>.Success(new ReAuctionCommandResult(reAuction.Id.Value, reAuction.AuctionNo.Value, auction.Id.Value));
  }

  public async Task<Result<AuctionActionResult>> Handle(CancelAuctionCommand command, CancellationToken cancellationToken)
  {
    var auction = await context.LoadAuctionAsync(command.Id, cancellationToken);
    await context.EnsurePropertyActiveAsync(auction.PropertyId, cancellationToken);
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
