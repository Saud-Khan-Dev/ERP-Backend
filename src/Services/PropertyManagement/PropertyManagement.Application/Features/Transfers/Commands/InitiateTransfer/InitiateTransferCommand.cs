using FluentValidation;

public sealed record TransferPartyInput(Guid OwnerId, TransferPartyRole Role, decimal SharePct);

/// ConfirmFarFuture: the clerk confirms a transfer dated more than 90 days ahead (PropertyTransfer.FarFutureDays).
public sealed record TransferInput(
  Guid TransferTypeId,
  DateOnly TransferDate,
  IReadOnlyList<TransferPartyInput> Parties,
  string? TransferReferenceNo = null,
  decimal? ConsiderationAmount = null,
  string? Relationship = null,
  string? Remarks = null,
  bool ConfirmFarFuture = false);

public sealed record InitiateTransferCommandResult(Guid Id, string TransferNo);

/// Starts a transfer (TRF-00001 is generated). Nothing changes hands until it is approved; approval registers it.
/// The transfer date may be in the future (it can be approved from that date).
public sealed record InitiateTransferCommand(Guid PropertyId, TransferInput Transfer) : ICommand<Result<InitiateTransferCommandResult>>;

public class TransferInputValidator : AbstractValidator<TransferInput>
{
  public TransferInputValidator()
  {
    RuleFor(x => x.TransferTypeId).NotEmpty();
    RuleFor(x => x.Parties).NotEmpty().WithMessage("A transfer needs its transferors and transferees.");
    RuleForEach(x => x.Parties).ChildRules(party =>
    {
      party.RuleFor(p => p.OwnerId).NotEmpty();
      party.RuleFor(p => p.Role).IsInEnum();
      party.RuleFor(p => p.SharePct).GreaterThan(0).LessThanOrEqualTo(100);
    });
    RuleFor(x => x.TransferReferenceNo).MaximumLength(100);
    RuleFor(x => x.Relationship).MaximumLength(100);
    RuleFor(x => x.ConsiderationAmount).GreaterThanOrEqualTo(0).When(x => x.ConsiderationAmount.HasValue);
  }
}

public class InitiateTransferCommandValidator : AbstractValidator<InitiateTransferCommand>
{
  public InitiateTransferCommandValidator()
  {
    RuleFor(x => x.PropertyId).NotEmpty();
    RuleFor(x => x.Transfer).NotNull().SetValidator(new TransferInputValidator());
  }
}
