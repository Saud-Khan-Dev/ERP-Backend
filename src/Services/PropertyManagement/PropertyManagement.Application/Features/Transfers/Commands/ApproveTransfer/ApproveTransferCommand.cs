using FluentValidation;

public sealed record ApproveTransferCommandResult(TransferStatus Status);

/// ApprovedBy defaults to the signed-in user's name (schema: approved_by is the approving officer's name).
public sealed record ApproveTransferCommand(Guid Id, string? ApprovedBy = null, DateOnly? ApprovalDate = null) : ICommand<Result<ApproveTransferCommandResult>>;

public class ApproveTransferCommandValidator : AbstractValidator<ApproveTransferCommand>
{
  public ApproveTransferCommandValidator()
  {
    RuleFor(x => x.Id).NotEmpty();
    RuleFor(x => x.ApprovedBy).MaximumLength(150);
  }
}
