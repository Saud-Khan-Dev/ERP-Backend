using FluentValidation;

public sealed record CancelTransferCommandResult(TransferStatus Status);

/// Withdraws a transfer that has not changed ownership: one waiting for approval (PROPERTY.EDIT), or one
/// approved before approval became completion (PROPERTY.APPROVE). The reason is required.
public sealed record CancelTransferCommand(Guid Id, string Reason) : ICommand<Result<CancelTransferCommandResult>>;

public class CancelTransferCommandValidator : AbstractValidator<CancelTransferCommand>
{
  public CancelTransferCommandValidator()
  {
    RuleFor(x => x.Id).NotEmpty();
    RuleFor(x => x.Reason).NotEmpty().WithMessage("Give the reason the transfer is cancelled.").MaximumLength(4000);
  }
}
