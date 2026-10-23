using FluentValidation;

/// Status: Completed (registered now) or Initiated with AwaitingApproval = true (the PROPERTY_TRANSFER rule is on: the
/// entry waits for another officer, nothing changed hands, the id lists are empty).
public sealed record RecordCompletedTransferCommandResult(
  Guid Id,
  string TransferNo,
  IReadOnlyList<Guid> ClosedOwnershipIds,
  IReadOnlyList<Guid> OpenedOwnershipIds,
  TransferStatus Status = TransferStatus.Completed,
  bool AwaitingApproval = false);

/// Enters a transfer that already happened (e.g. from the paper register). It runs the same rules as initiate → approve,
/// and the transfer date may not be in the future. While the PROPERTY_TRANSFER separation rule is on (the default) the
/// entry is saved waiting for approval with the approval read off the paper kept on it, and another officer approves it;
/// with the rule off it is COMPLETED in one step, the signed-in officer as approver. ApprovalDate defaults to the
/// transfer date.
public sealed record RecordCompletedTransferCommand(
  Guid PropertyId,
  TransferInput Transfer,
  DateOnly? ApprovalDate = null,
  Guid? TenureTypeId = null,
  string? ApprovalOrderRef = null,
  string? NoDuesCertificateNo = null) : ICommand<Result<RecordCompletedTransferCommandResult>>;

public class RecordCompletedTransferCommandValidator : AbstractValidator<RecordCompletedTransferCommand>
{
  public RecordCompletedTransferCommandValidator()
  {
    RuleFor(x => x.PropertyId).NotEmpty();
    RuleFor(x => x.ApprovalOrderRef).MaximumLength(100);
    RuleFor(x => x.NoDuesCertificateNo).MaximumLength(PropertyTransfer.NoDuesCertificateMaxLength);
    RuleFor(x => x.Transfer).NotNull().SetValidator(new TransferInputValidator());
  }
}
