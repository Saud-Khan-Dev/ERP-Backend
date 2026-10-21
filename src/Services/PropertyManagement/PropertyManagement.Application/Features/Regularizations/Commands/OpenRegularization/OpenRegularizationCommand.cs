using FluentValidation;

/// ApprovedBy / ApprovedByEmployeeId: the approving officer picked from the staff list (the HR record and the name as
/// it stood then); a decided status (Regularized / Rejected) needs PROPERTY.APPROVE.
public sealed record RegularizationInput(
  decimal AdditionalArea,
  Guid MeasurementUnitId,
  RegularizationStatus Status = RegularizationStatus.Applied,
  DateOnly? ApplicationDate = null,
  DateOnly? RegularizationDate = null,
  string? OrderReferenceNo = null,
  string? ApprovedBy = null,
  DateOnly? EffectiveFrom = null,
  string? Remarks = null,
  Guid? ApprovedByEmployeeId = null);

public sealed record OpenRegularizationCommandResult(Guid Id);

public sealed record OpenRegularizationCommand(Guid PropertyId, RegularizationInput Regularization) : ICommand<Result<OpenRegularizationCommandResult>>;

public class OpenRegularizationCommandValidator : AbstractValidator<OpenRegularizationCommand>
{
  public OpenRegularizationCommandValidator()
  {
    RuleFor(x => x.PropertyId).NotEmpty();
    RuleFor(x => x.Regularization).NotNull();
    RuleFor(x => x.Regularization.AdditionalArea).GreaterThan(0);
    RuleFor(x => x.Regularization.MeasurementUnitId).NotEmpty();
    RuleFor(x => x.Regularization.Status).IsInEnum();
    RuleFor(x => x.Regularization.OrderReferenceNo).MaximumLength(100);
    RuleFor(x => x.Regularization.ApprovedBy).MaximumLength(PropertyAreaRegularization.ApprovedByMaxLength);
    RuleFor(x => x.Regularization.Remarks).MaximumLength(4000);
  }
}

public static class RegularizationDecisions
{
  /// Regularized and Rejected decide the case: an approver's act.
  public static void EnsureMayDecide(ICurrentUser currentUser, RegularizationStatus status, RegularizationStatus? before = null)
  {
    if (PropertyAreaRegularization.IsDecided(status) && status != before && !currentUser.HasPermission(PermissionCatalog.Property.Approve))
      throw new PermissionRequiredException(
        $"Marking a regularization case {(status == RegularizationStatus.Regularized ? "regularized" : "rejected")} is an approver's decision; ask an officer who can approve.");
  }
}
