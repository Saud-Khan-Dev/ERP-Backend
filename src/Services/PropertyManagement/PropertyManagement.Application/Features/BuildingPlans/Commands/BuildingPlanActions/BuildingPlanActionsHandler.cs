public class BuildingPlanActionsHandler(IApplicationDbContext context, MasterLookup masters, ICurrentUser currentUser)
  : ICommandHandler<UpdateBuildingPlanCommand, Result<BuildingPlanActionResult>>,
    ICommandHandler<ChangeBuildingPlanStatusCommand, Result<BuildingPlanActionResult>>,
    ICommandHandler<ApproveBuildingPlanCommand, Result<BuildingPlanActionResult>>,
    ICommandHandler<RejectBuildingPlanCommand, Result<BuildingPlanActionResult>>,
    ICommandHandler<ReviseBuildingPlanCommand, Result<ReviseBuildingPlanCommandResult>>
{
  public async Task<Result<BuildingPlanActionResult>> Handle(UpdateBuildingPlanCommand command, CancellationToken cancellationToken)
  {
    var plan = await context.LoadBuildingPlanAsync(command.Id, cancellationToken);
    plan.Update(await Current(plan, cancellationToken), await command.Plan.ToDetailsAsync(context, masters, cancellationToken));
    return await SaveAsync(plan, cancellationToken);
  }

  public async Task<Result<BuildingPlanActionResult>> Handle(ChangeBuildingPlanStatusCommand command, CancellationToken cancellationToken)
  {
    var plan = await context.LoadBuildingPlanAsync(command.Id, cancellationToken);
    plan.ChangeStatus(await Current(plan, cancellationToken), await masters.GetAsync<BuildingPlanStatus>(command.BuildingPlanStatusId, cancellationToken));
    return await SaveAsync(plan, cancellationToken);
  }

  public async Task<Result<BuildingPlanActionResult>> Handle(ApproveBuildingPlanCommand command, CancellationToken cancellationToken)
  {
    var plan = await context.LoadBuildingPlanAsync(command.Id, cancellationToken);
    plan.Approve(await Current(plan, cancellationToken), await Status(SystemMasterCodes.Approved, cancellationToken),
      command.ApprovalDate, command.ApprovedBy ?? currentUser.Username ?? currentUser.AuditName, command.ApprovalReferenceNo, command.ValidityEndDate);
    return await SaveAsync(plan, cancellationToken);
  }

  public async Task<Result<BuildingPlanActionResult>> Handle(RejectBuildingPlanCommand command, CancellationToken cancellationToken)
  {
    var plan = await context.LoadBuildingPlanAsync(command.Id, cancellationToken);
    plan.Reject(await Current(plan, cancellationToken), await Status(SystemMasterCodes.Rejected, cancellationToken), command.Remarks);
    return await SaveAsync(plan, cancellationToken);
  }

  public async Task<Result<ReviseBuildingPlanCommandResult>> Handle(ReviseBuildingPlanCommand command, CancellationToken cancellationToken)
  {
    var plan = await context.LoadBuildingPlanAsync(command.Id, cancellationToken);

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

  private async Task<Result<BuildingPlanActionResult>> SaveAsync(BuildingPlan plan, CancellationToken cancellationToken)
  {
    await context.SaveChangesAsync(cancellationToken);
    var refs = await masters.Refs().Add<BuildingPlanStatus>(plan.BuildingPlanStatusId).LoadAsync(cancellationToken);
    return Result<BuildingPlanActionResult>.Success(new BuildingPlanActionResult(plan.Id.Value, refs[plan.BuildingPlanStatusId]));
  }
}
