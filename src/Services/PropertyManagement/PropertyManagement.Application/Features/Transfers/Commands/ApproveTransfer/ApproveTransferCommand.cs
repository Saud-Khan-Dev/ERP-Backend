using FluentValidation;

/// Approving a transfer registers it: ClosedOwnershipIds are the rows ended on the transfer date,
/// OpenedOwnershipIds the rows that now hold the shares.
public sealed record ApproveTransferCommandResult(
  TransferStatus Status,
  IReadOnlyList<Guid> ClosedOwnershipIds,
  IReadOnlyList<Guid> OpenedOwnershipIds);

/// Approves AND completes an INITIATED transfer in one step (the only waiting state is "pending approval").
/// The approver is the signed-in officer (never typed); while the PROPERTY_TRANSFER separation rule is on the person who
/// started (or recorded) the transfer cannot approve it. ApprovalDate defaults to the one recorded with a past transfer,
/// else today; the order ref and certificate default to the recorded ones too. NoDuesCertificateNo: the Taxation clearance the
/// approval relied on, when given. TenureTypeId is the tenure for transferees who owned nothing before (defaults to OWNED).
public sealed record ApproveTransferCommand(
  Guid Id,
  DateOnly? ApprovalDate = null,
  string? ApprovalOrderRef = null,
  Guid? TenureTypeId = null,
  string? NoDuesCertificateNo = null) : ICommand<Result<ApproveTransferCommandResult>>;

public class ApproveTransferCommandValidator : AbstractValidator<ApproveTransferCommand>
{
  public ApproveTransferCommandValidator()
  {
    RuleFor(x => x.Id).NotEmpty();
    RuleFor(x => x.ApprovalOrderRef).MaximumLength(100);
    RuleFor(x => x.NoDuesCertificateNo).MaximumLength(PropertyTransfer.NoDuesCertificateMaxLength);
  }
}

/// Shared by the acts that register a transfer.
public static class TransferRegistration
{
  /// The tenure a transferee who owned nothing before receives: the one asked for, else OWNED.
  public static async Task<TenureType> NewcomerTenureAsync(MasterLookup masters, Guid? tenureTypeId, CancellationToken cancellationToken) =>
      tenureTypeId is { } id
        ? await masters.GetAsync<TenureType>(id, cancellationToken)
        : await masters.GetByCodeAsync<TenureType>(SystemMasterCodes.TenureOwned, cancellationToken);

  /// The name written to completed_by / approved_by / closed_by for the signed-in user.
  public static string ActorName(ICurrentUser currentUser) => currentUser.AuditName;

  /// The signed-in officer as the approver of an act.
  public static Approver Approver(ICurrentUser currentUser) => new(currentUser.UserId, currentUser.EmployeeId, ActorName(currentUser));
}
