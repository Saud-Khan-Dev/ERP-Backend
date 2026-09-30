using FluentValidation;

public sealed record RecordCompletedTransferCommandResult(
  Guid Id,
  string TransferNo,
  IReadOnlyList<Guid> ClosedOwnershipIds,
  IReadOnlyList<Guid> OpenedOwnershipIds);

/// Enters a transfer that already happened (e.g. from the paper register) as COMPLETED in one step —
/// the ERD's default transfer_status. It runs the same rules as initiate → approve → complete, so the
/// ownership rows are closed and opened exactly as for a new transfer.
/// ApprovalDate defaults to the transfer date; ApprovedBy to the signed-in user.
public sealed record RecordCompletedTransferCommand(
  Guid PropertyId,
  TransferInput Transfer,
  string? ApprovedBy = null,
  DateOnly? ApprovalDate = null,
  Guid? TenureTypeId = null) : ICommand<Result<RecordCompletedTransferCommandResult>>;

public class RecordCompletedTransferCommandValidator : AbstractValidator<RecordCompletedTransferCommand>
{
  public RecordCompletedTransferCommandValidator()
  {
    RuleFor(x => x.PropertyId).NotEmpty();
    RuleFor(x => x.ApprovedBy).MaximumLength(150);
    RuleFor(x => x.Transfer).NotNull().SetValidator(new TransferInputValidator());
  }
}
