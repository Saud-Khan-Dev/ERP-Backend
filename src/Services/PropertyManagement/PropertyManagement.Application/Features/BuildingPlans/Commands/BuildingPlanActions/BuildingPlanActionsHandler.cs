using Microsoft.EntityFrameworkCore;

public class BuildingPlanActionsHandler(IApplicationDbContext context, MasterLookup masters, ICurrentUser currentUser, IAuditTrail audit)
  : ICommandHandler<UpdateBuildingPlanCommand, Result<BuildingPlanActionResult>>,
    ICommandHandler<ChangeBuildingPlanStatusCommand, Result<BuildingPlanActionResult>>,
    ICommandHandler<ApproveBuildingPlanCommand, Result<BuildingPlanActionResult>>,
    ICommandHandler<RejectBuildingPlanCommand, Result<BuildingPlanActionResult>>,
    ICommandHandler<ReviseBuildingPlanCommand, Result<ReviseBuildingPlanCommandResult>>
{
  public async Task<Result<BuildingPlanActionResult>> Handle(UpdateBuildingPlanCommand command, CancellationToken cancellationToken)
  {
    var plan = await context.LoadBuildingPlanAsync(command.Id, cancellationToken);
    await context.EnsurePropertyActiveAsync(plan.PropertyId, cancellationToken);
    plan.Update(await Current(plan, cancellationToken), await command.Plan.ToDetailsAsync(context, masters, cancellationToken));
    return await SaveAsync(plan, cancellationToken);
  }

  public async Task<Result<BuildingPlanActionResult>> Handle(ChangeBuildingPlanStatusCommand command, CancellationToken cancellationToken)
  {
    var plan = await context.LoadBuildingPlanAsync(command.Id, cancellationToken);
    await context.EnsurePropertyActiveAsync(plan.PropertyId, cancellationToken);
    plan.ChangeStatus(await Current(plan, cancellationToken), await masters.GetAsync<BuildingPlanStatus>(command.BuildingPlanStatusId, cancellationToken));
    return await SaveAsync(plan, cancellationToken);
  }

  public async Task<Result<BuildingPlanActionResult>> Handle(ApproveBuildingPlanCommand command, CancellationToken cancellationToken)
  {
    var plan = await context.LoadBuildingPlanAsync(command.Id, cancellationToken);
    await context.EnsurePropertyActiveAsync(plan.PropertyId, cancellationToken);

    // the plot's current survey: a slope above the BCR limit approves with a warning, never refused
    var slope = await context.Boundaries.AsNoTracking()
        .Where(b => b.PropertyId == plan.PropertyId && b.IsCurrent)
        .OrderByDescending(b => b.SurveyDate)
        .Select(b => b.SlopeDegrees)
        .FirstOrDefaultAsync(cancellationToken);

    // the approving officer picked from the staff list, else the signed-in officer
    var picked = !string.IsNullOrWhiteSpace(command.ApprovedBy);
    var warnings = plan.Approve(await Current(plan, cancellationToken), await Status(SystemMasterCodes.Approved, cancellationToken),
      new BuildingPlan.Approval(
        command.ApprovalDate,
        picked ? command.ApprovedBy! : currentUser.AuditName,
        picked ? command.ApprovedByEmployeeId : currentUser.EmployeeId,
        command.ApprovalReferenceNo, command.ValidityEndDate, command.NoDuesCertificateNo),
      PropertyWriteGuard.Today(), slope);
    audit.Record(PropertyAuditActions.Approved, plan, string.Join(" · ", new[] { $"approving officer on file: {plan.ApprovedBy}", command.ApprovalReferenceNo is { } reference ? $"order {reference}" : null, command.NoDuesCertificateNo is { } certificate ? $"no-dues certificate {certificate}" : null }.Where(x => x is not null)));
    return await SaveAsync(plan, cancellationToken, warnings);
  }

  public async Task<Result<BuildingPlanActionResult>> Handle(RejectBuildingPlanCommand command, CancellationToken cancellationToken)
  {
    var plan = await context.LoadBuildingPlanAsync(command.Id, cancellationToken);
    await context.EnsurePropertyActiveAsync(plan.PropertyId, cancellationToken);
    plan.Reject(await Current(plan, cancellationToken), await Status(SystemMasterCodes.Rejected, cancellationToken), command.Remarks);
    audit.Record(PropertyAuditActions.Rejected, plan, command.Remarks);
    return await SaveAsync(plan, cancellationToken);
  }

  public async Task<Result<ReviseBuildingPlanCommandResult>> Handle(ReviseBuildingPlanCommand command, CancellationToken cancellationToken)
  {
    var plan = await context.LoadBuildingPlanAsync(command.Id, cancellationToken);
    await context.EnsurePropertyActiveAsync(plan.PropertyId, cancellationToken);

    var revision = plan.Revise(
      await Current(plan, cancellationToken),
      await Status(SystemMasterCodes.Revised, cancellationToken),
      await Status(SystemMasterCodes.Submitted, cancellationToken),
      BuildingPlanId.New(),
      await command.Plan.ToDetailsAsync(context, masters, cancellationToken));

    await context.BuildingPlans.AddAsync(revision, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    return Result<ReviseBuildingPlanCommandResult>.Success(
      new ReviseBuildingPlanCommandResult(revision.Id.Value, revision.PlanNo.Value, revision.RevisionNo, plan.Id.Value));
  }

  private Task<BuildingPlanStatus> Current(BuildingPlan plan, CancellationToken cancellationToken) =>
      masters.GetAsync<BuildingPlanStatus>(plan.BuildingPlanStatusId.Value, cancellationToken);

  private Task<BuildingPlanStatus> Status(string code, CancellationToken cancellationToken) =>
      masters.GetByCodeAsync<BuildingPlanStatus>(code, cancellationToken);

  private async Task<Result<BuildingPlanActionResult>> SaveAsync(BuildingPlan plan, CancellationToken cancellationToken, IReadOnlyList<string>? warnings = null)
  {
    await context.SaveChangesAsync(cancellationToken);
    var refs = await masters.Refs().Add<BuildingPlanStatus>(plan.BuildingPlanStatusId).LoadAsync(cancellationToken);
    return Result<BuildingPlanActionResult>.Success(new BuildingPlanActionResult(plan.Id.Value, refs[plan.BuildingPlanStatusId], warnings ?? Array.Empty<string>()));
  }
}
