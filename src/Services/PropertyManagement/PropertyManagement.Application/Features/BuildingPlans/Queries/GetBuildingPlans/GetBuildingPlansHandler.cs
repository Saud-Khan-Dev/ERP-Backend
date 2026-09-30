using Microsoft.EntityFrameworkCore;

public class GetBuildingPlansHandler(IApplicationDbContext context, MasterLookup masters, PropertyReadService read)
  : IQueryHandler<GetPropertyBuildingPlansQuery, Result<GetBuildingPlansQueryResult>>,
    IQueryHandler<GetBuildingPlanQuery, Result<GetBuildingPlanQueryResult>>
{
  public async Task<Result<GetBuildingPlansQueryResult>> Handle(GetPropertyBuildingPlansQuery query, CancellationToken cancellationToken)
  {
    var propertyId = await context.EnsurePropertyExistsAsync(query.PropertyId, cancellationToken);
    var rows = await context.BuildingPlans.AsNoTracking().Where(p => p.PropertyId == propertyId)
        .OrderBy(p => p.PlanNo).ThenByDescending(p => p.RevisionNo).ToListAsync(cancellationToken);

    if (!query.IncludeRevisions)
    {
      var superseded = rows.Where(p => p.SupersedesPlanId != null).Select(p => p.SupersedesPlanId!).ToHashSet();
      rows = rows.Where(p => !superseded.Contains(p.Id)).ToList();
    }

    return Result<GetBuildingPlansQueryResult>.Success(new GetBuildingPlansQueryResult(await ToDtosAsync(rows, cancellationToken)));
  }

  public async Task<Result<GetBuildingPlanQueryResult>> Handle(GetBuildingPlanQuery query, CancellationToken cancellationToken)
  {
    var plan = await context.LoadBuildingPlanAsync(query.Id, cancellationToken);
    return Result<GetBuildingPlanQueryResult>.Success(new GetBuildingPlanQueryResult((await ToDtosAsync(new[] { plan }, cancellationToken))[0]));
  }

  private async Task<List<BuildingPlanDto>> ToDtosAsync(IReadOnlyCollection<BuildingPlan> rows, CancellationToken cancellationToken)
  {
    var refs = await masters.Refs()
        .Add<BuildingPlanType>(rows.Select(p => p.BuildingPlanTypeId))
        .Add<BuildingPlanStatus>(rows.Select(p => p.BuildingPlanStatusId))
        .Add<MeasurementUnit>(rows.Select(p => p.MeasurementUnitId))
        .LoadAsync(cancellationToken);
    var owners = await read.OwnerRefsAsync(rows.Select(p => p.ApplicantOwnerId), cancellationToken);
    return rows.Select(p => p.ToDto(refs, owners)).ToList();
  }
}
