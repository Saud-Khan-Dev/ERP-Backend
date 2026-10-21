using FluentValidation;

/// Warnings: an approval of a plot surveyed steeper than the BCR 2026 limit.
public sealed record BuildingPlanActionResult(Guid Id, MasterRef? Status, IReadOnlyList<string>? Warnings = null);
public sealed record ReviseBuildingPlanCommandResult(Guid Id, string PlanNo, int RevisionNo, Guid SupersedesPlanId);

public sealed record UpdateBuildingPlanCommand(Guid Id, BuildingPlanInput Plan) : ICommand<Result<BuildingPlanActionResult>>;

/// Under Review, Withdrawn ...
public sealed record ChangeBuildingPlanStatusCommand(Guid Id, Guid BuildingPlanStatusId) : ICommand<Result<BuildingPlanActionResult>>;

/// An approver approves the plan (not dated in the future). ApprovedBy / ApprovedByEmployeeId: the approving officer
/// picked from the staff list; without one, the signed-in officer. NoDuesCertificateNo: the Taxation clearance relied on.
public sealed record ApproveBuildingPlanCommand(
  Guid Id,
  DateOnly ApprovalDate,
  string? ApprovedBy = null,
  string? ApprovalReferenceNo = null,
  DateOnly? ValidityEndDate = null,
  Guid? ApprovedByEmployeeId = null,
  string? NoDuesCertificateNo = null) : ICommand<Result<BuildingPlanActionResult>>;

public sealed record RejectBuildingPlanCommand(Guid Id, string? Remarks = null) : ICommand<Result<BuildingPlanActionResult>>;

/// A revised plan: same plan number, next revision; this revision becomes REVISED.
public sealed record ReviseBuildingPlanCommand(Guid Id, BuildingPlanInput Plan) : ICommand<Result<ReviseBuildingPlanCommandResult>>;

public class UpdateBuildingPlanCommandValidator : AbstractValidator<UpdateBuildingPlanCommand>
{
  public UpdateBuildingPlanCommandValidator() => RuleFor(x => x.Plan).NotNull().SetValidator(new BuildingPlanInputValidator());
}

public class ReviseBuildingPlanCommandValidator : AbstractValidator<ReviseBuildingPlanCommand>
{
  public ReviseBuildingPlanCommandValidator() => RuleFor(x => x.Plan).NotNull().SetValidator(new BuildingPlanInputValidator());
}

public class ApproveBuildingPlanCommandValidator : AbstractValidator<ApproveBuildingPlanCommand>
{
  public ApproveBuildingPlanCommandValidator()
  {
    RuleFor(x => x.ApprovedBy).MaximumLength(150);
    RuleFor(x => x.ApprovalReferenceNo).MaximumLength(100);
    RuleFor(x => x.NoDuesCertificateNo).MaximumLength(BuildingPlan.NoDuesCertificateMaxLength);
  }
}
