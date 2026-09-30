using FluentValidation;

public sealed record AuctionActionResult(Guid Id, MasterRef? Status);
public sealed record RecordBidCommandResult(Guid BidId, int? BidRank);

public sealed record UpdateAuctionCommand(Guid Id, AuctionDetailsInput Auction) : ICommand<Result<AuctionActionResult>>;

/// Announced, Conducted, Successful, Unsuccessful ...
public sealed record ChangeAuctionStatusCommand(Guid Id, Guid AuctionStatusId) : ICommand<Result<AuctionActionResult>>;

public sealed record RecordBidCommand(Guid Id, Guid BidderOwnerId, decimal BidAmount, decimal? EarnestMoney = null, string? Remarks = null) : ICommand<Result<RecordBidCommandResult>>;

/// With recorded bids: WinningBidId (default: the highest). Without: SuccessfulBidderOwnerId + WinningBidAmount.
public sealed record AwardAuctionCommand(
  Guid Id,
  DateOnly AwardDate,
  Guid? WinningBidId = null,
  Guid? SuccessfulBidderOwnerId = null,
  decimal? WinningBidAmount = null,
  string? AwardReferenceNo = null) : ICommand<Result<AuctionActionResult>>;

public sealed record CancelAuctionCommand(Guid Id, string? Remarks = null) : ICommand<Result<AuctionActionResult>>;

public class UpdateAuctionCommandValidator : AbstractValidator<UpdateAuctionCommand>
{
  public UpdateAuctionCommandValidator() => RuleFor(x => x.Auction).NotNull().SetValidator(new AuctionDetailsInputValidator());
}

public class RecordBidCommandValidator : AbstractValidator<RecordBidCommand>
{
  public RecordBidCommandValidator()
  {
    RuleFor(x => x.BidderOwnerId).NotEmpty();
    RuleFor(x => x.BidAmount).GreaterThan(0);
    RuleFor(x => x.EarnestMoney).GreaterThanOrEqualTo(0).When(x => x.EarnestMoney.HasValue);
    RuleFor(x => x.Remarks).MaximumLength(300);
  }
}
