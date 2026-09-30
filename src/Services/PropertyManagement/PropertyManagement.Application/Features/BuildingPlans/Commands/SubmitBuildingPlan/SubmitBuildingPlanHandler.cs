public class SubmitBuildingPlanHandler(IApplicationDbContext context, MasterLookup masters, CodeGenerator codes)
  : ICommandHandler<SubmitBuildingPlanCommand, Result<SubmitBuildingPlanCommandResult>>
{
  public async Task<Result<SubmitBuildingPlanCommandResult>> Handle(SubmitBuildingPlanCommand command, CancellationToken cancellationToken)
  {
    var property = await context.LoadPropertyAsync(command.PropertyId, cancellationToken);

    var plan = BuildingPlan.Submit(
      BuildingPlanId.New(), property, await codes.NextAsync(CodeSequenceKeys.BuildingPlan, cancellationToken),
      await masters.GetByCodeAsync<BuildingPlanStatus>(SystemMasterCodes.Submitted, cancellationToken),
      await command.Plan.ToDetailsAsync(context, masters, cancellationToken));

    await context.BuildingPlans.AddAsync(plan, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    return Result<SubmitBuildingPlanCommandResult>.Success(new SubmitBuildingPlanCommandResult(plan.Id.Value, plan.PlanNo.Value, plan.RevisionNo));
  }
}
