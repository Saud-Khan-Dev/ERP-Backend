using FluentValidation;

public sealed record AuctionDetailsInput(
  Guid AuctionTypeId,
  DateOnly? AnnouncementDate = null,
  DateOnly? AuctionDate = null,
  decimal? BaseReservePrice = null,
  string? AuctionCommitteeRef = null,
  string? Venue = null,
  string? ReferenceNo = null,
  string? Remarks = null)
{
  public async Task<PropertyAuction.Details> ToDetailsAsync(MasterLookup masters, CancellationToken cancellationToken) => new(
    await masters.GetAsync<AuctionType>(AuctionTypeId, cancellationToken),
    AnnouncementDate, AuctionDate, BaseReservePrice, AuctionCommitteeRef, Venue, ReferenceNo, Remarks);
}

public class AuctionDetailsInputValidator : AbstractValidator<AuctionDetailsInput>
{
  public AuctionDetailsInputValidator()
  {
    RuleFor(x => x.AuctionTypeId).NotEmpty();
    RuleFor(x => x.BaseReservePrice).GreaterThanOrEqualTo(0).When(x => x.BaseReservePrice.HasValue);
    RuleFor(x => x.AuctionCommitteeRef).MaximumLength(100);
    RuleFor(x => x.Venue).MaximumLength(200);
    RuleFor(x => x.ReferenceNo).MaximumLength(100);
  }
}

public sealed record PlanAuctionCommandResult(Guid Id, string AuctionNo);

/// AUC-00001 is generated; the auction starts PLANNED.
public sealed record PlanAuctionCommand(Guid PropertyId, AuctionDetailsInput Auction) : ICommand<Result<PlanAuctionCommandResult>>;

public class PlanAuctionCommandValidator : AbstractValidator<PlanAuctionCommand>
{
  public PlanAuctionCommandValidator()
  {
    RuleFor(x => x.PropertyId).NotEmpty();
    RuleFor(x => x.Auction).NotNull().SetValidator(new AuctionDetailsInputValidator());
  }
}
