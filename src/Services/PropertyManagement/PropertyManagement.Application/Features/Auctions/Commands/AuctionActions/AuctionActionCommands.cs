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

public sealed record ReAuctionCommandResult(Guid Id, string AuctionNo, Guid PreviousAuctionId);

/// Re-auctions an UNSUCCESSFUL or CANCELLED auction: a new PLANNED auction (AUC-…) of type RE_AUCTION on the
/// same property, with previousAuctionId pointing back. BaseReservePrice defaults to the previous auction's.
public sealed record ReAuctionCommand(Guid Id, DateOnly AuctionDate, decimal? BaseReservePrice = null, string? AuctionCommitteeRef = null)
  : ICommand<Result<ReAuctionCommandResult>>;

public class ReAuctionCommandValidator : AbstractValidator<ReAuctionCommand>
{
  public ReAuctionCommandValidator()
  {
    RuleFor(x => x.BaseReservePrice).GreaterThanOrEqualTo(0).When(x => x.BaseReservePrice.HasValue);
    RuleFor(x => x.AuctionCommitteeRef).MaximumLength(PropertyAuction.ReferenceMaxLength);
  }
}

public class AwardAuctionCommandValidator : AbstractValidator<AwardAuctionCommand>
{
  public AwardAuctionCommandValidator()
  {
    RuleFor(x => x.AwardDate).LessThanOrEqualTo(_ => PropertyWriteGuard.Today()).WithMessage("An award cannot be dated in the future.");
    RuleFor(x => x.WinningBidAmount).GreaterThan(0).When(x => x.WinningBidAmount.HasValue);
    RuleFor(x => x.AwardReferenceNo).MaximumLength(PropertyAuction.ReferenceMaxLength);
  }
}

public class CancelAuctionCommandValidator : AbstractValidator<CancelAuctionCommand>
{
  public CancelAuctionCommandValidator() => RuleFor(x => x.Remarks).MaximumLength(PropertyAuction.RemarksMaxLength);
}

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
