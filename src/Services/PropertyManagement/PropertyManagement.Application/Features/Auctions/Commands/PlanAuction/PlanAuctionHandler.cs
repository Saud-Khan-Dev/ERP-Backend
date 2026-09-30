public class PlanAuctionHandler(IApplicationDbContext context, MasterLookup masters, CodeGenerator codes)
  : ICommandHandler<PlanAuctionCommand, Result<PlanAuctionCommandResult>>
{
  public async Task<Result<PlanAuctionCommandResult>> Handle(PlanAuctionCommand command, CancellationToken cancellationToken)
  {
    var property = await context.LoadPropertyAsync(command.PropertyId, cancellationToken);

    var auction = PropertyAuction.Plan(
      AuctionId.New(), property, await codes.NextAsync(CodeSequenceKeys.Auction, cancellationToken),
      await masters.GetByCodeAsync<AuctionStatus>(SystemMasterCodes.Planned, cancellationToken),
      await command.Auction.ToDetailsAsync(masters, cancellationToken));

    await context.Auctions.AddAsync(auction, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    return Result<PlanAuctionCommandResult>.Success(new PlanAuctionCommandResult(auction.Id.Value, auction.AuctionNo.Value));
  }
}
