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

    // an auction of a property that is let, contracted out or allotted is allowed (the agreement may end first) but said
    var warnings = (await context.RunningOnAsync(property.Id, cancellationToken))
      .Select(s => $"{property.PropertyCode.Value} is {s.Kind switch { AgreementSpanKind.Allotment => "allotted to", AgreementSpanKind.Lease => "leased to", AgreementSpanKind.Rental => "rented to", _ => "under contract with" }} "
        + $"{s.PartyName} ({s.Code}){(s.End is { } end ? $" until {end:yyyy-MM-dd}" : " with no end date")}; settle that before the property changes hands.")
      .ToList();

    await context.Auctions.AddAsync(auction, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    return Result<PlanAuctionCommandResult>.Success(new PlanAuctionCommandResult(auction.Id.Value, auction.AuctionNo.Value, warnings));
  }
}
