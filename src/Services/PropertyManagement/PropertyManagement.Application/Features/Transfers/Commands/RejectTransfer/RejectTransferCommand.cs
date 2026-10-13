using FluentValidation;

public sealed record RejectTransferCommandResult(TransferStatus Status);

/// The approving officer refuses a transfer waiting for approval. Nothing changes hands; the reason is required.
public sealed record RejectTransferCommand(Guid Id, string Reason) : ICommand<Result<RejectTransferCommandResult>>;

public class RejectTransferCommandValidator : AbstractValidator<RejectTransferCommand>
{
  public RejectTransferCommandValidator()
  {
    RuleFor(x => x.Id).NotEmpty();
    RuleFor(x => x.Reason).NotEmpty().WithMessage("Give the reason the transfer is rejected.").MaximumLength(4000);
  }
}
