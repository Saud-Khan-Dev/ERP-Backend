using FluentValidation;

public sealed record CompleteTransferCommandResult(IReadOnlyList<Guid> ClosedOwnershipIds, IReadOnlyList<Guid> OpenedOwnershipIds);

/// Registers a transfer that was APPROVED before approval and completion became one step (existing rows only):
/// the parties' current rows are closed and new rows opened. A new transfer is registered by approving it.
/// TenureTypeId is the tenure for transferees who owned nothing before (defaults to OWNED).
public sealed record CompleteTransferCommand(Guid Id, Guid? TenureTypeId = null, string? ApprovalOrderRef = null) : ICommand<Result<CompleteTransferCommandResult>>;

public class CompleteTransferCommandValidator : AbstractValidator<CompleteTransferCommand>
{
  public CompleteTransferCommandValidator()
  {
    RuleFor(x => x.Id).NotEmpty();
    RuleFor(x => x.ApprovalOrderRef).MaximumLength(100);
  }
}
