using FluentValidation;

/// Approving a transfer registers it: ClosedOwnershipIds are the rows ended on the transfer date,
/// OpenedOwnershipIds the rows that now hold the shares.
public sealed record ApproveTransferCommandResult(
  TransferStatus Status,
  IReadOnlyList<Guid> ClosedOwnershipIds,
  IReadOnlyList<Guid> OpenedOwnershipIds);

/// Approves AND completes an INITIATED transfer in one step (the only waiting state is "pending approval").
/// ApprovedBy defaults to the signed-in user's name (schema: approved_by is the approving officer's name);
/// ApprovalDate to today. TenureTypeId is the tenure for transferees who owned nothing before (defaults to OWNED).
public sealed record ApproveTransferCommand(
  Guid Id,
  string? ApprovedBy = null,
  DateOnly? ApprovalDate = null,
  string? ApprovalOrderRef = null,
  Guid? TenureTypeId = null) : ICommand<Result<ApproveTransferCommandResult>>;

public class ApproveTransferCommandValidator : AbstractValidator<ApproveTransferCommand>
{
  public ApproveTransferCommandValidator()
  {
    RuleFor(x => x.Id).NotEmpty();
    RuleFor(x => x.ApprovedBy).MaximumLength(150);
    RuleFor(x => x.ApprovalOrderRef).MaximumLength(100);
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
  public static string ActorName(ICurrentUser currentUser) => currentUser.Username ?? currentUser.AuditName;
}
