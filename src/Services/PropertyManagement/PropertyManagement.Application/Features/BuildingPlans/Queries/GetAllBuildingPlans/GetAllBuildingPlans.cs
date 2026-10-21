using FluentValidation;
using Microsoft.EntityFrameworkCore;

public enum BuildingPlanSort { Submitted, Code, Property, Status }

public sealed record GetAllBuildingPlansQueryResult(PaginatedResult<BuildingPlanDto> Plans);

/// Building plans across all active properties (the latest revision of each plan), paged. AwaitingDecision: true = not
/// yet approved, rejected, revised or withdrawn (the approvers' queue), false = decided. Statuses: status codes, any of
/// them. Search: part of the plan no., the property's code or name, or the applicant. Default order: the oldest
/// submission first (the longest waiting), rows without a submission date last.
public sealed record GetAllBuildingPlansQuery(
  PaginationRequest Pagination,
  bool? AwaitingDecision = null,
  IReadOnlyList<string>? Statuses = null,
  Guid? PropertyId = null,
  Guid? TownId = null,
  string? Search = null,
  BuildingPlanSort SortBy = BuildingPlanSort.Submitted,
  bool SortDescending = false) : IQuery<Result<GetAllBuildingPlansQueryResult>>;

public class GetAllBuildingPlansQueryValidator : AbstractValidator<GetAllBuildingPlansQuery>
{
  public GetAllBuildingPlansQueryValidator()
  {
    RuleFor(x => x.Pagination.Pageindex).GreaterThanOrEqualTo(0);
    RuleFor(x => x.Pagination.PageSize).InclusiveBetween(1, 5000);
    RuleFor(x => x.SortBy).IsInEnum();
  }
}

public class GetAllBuildingPlansHandler(IApplicationDbContext context, MasterLookup masters, PropertyReadService read)
  : IQueryHandler<GetAllBuildingPlansQuery, Result<GetAllBuildingPlansQueryResult>>
{
  public async Task<Result<GetAllBuildingPlansQueryResult>> Handle(GetAllBuildingPlansQuery query, CancellationToken cancellationToken)
  {
    var scope = RegisterScope.Create(context, query.PropertyId, query.TownId, query.Search);
    var superseded = context.BuildingPlans.AsNoTracking().Where(p => p.SupersedesPlanId != null).Select(p => p.SupersedesPlanId!);
    var rows = context.BuildingPlans.AsNoTracking().Where(p => !superseded.Contains(p.Id));

    if (scope.Properties is { } allowed) rows = rows.Where(p => allowed.Contains(p.PropertyId));

    if (query.AwaitingDecision is { } awaiting)
    {
      var decided = await InForce.StatusIdsAsync<BuildingPlanStatus>(context, BuildingPlan.ClosedStatusCodes, cancellationToken);
      rows = awaiting ? rows.Where(p => !decided.Contains(p.BuildingPlanStatusId)) : rows.Where(p => decided.Contains(p.BuildingPlanStatusId));
    }

    if (query.Statuses is { Count: > 0 })
    {
      var statuses = await InForce.StatusIdsAsync<BuildingPlanStatus>(context, query.Statuses.Select(s => s.Trim().ToUpperInvariant()).ToList(), cancellationToken);
      rows = rows.Where(p => statuses.Contains(p.BuildingPlanStatusId));
    }

    if (scope.HasSearch)
    {
      var (upper, matchingProperties, matchingOwners) = (scope.Upper!, scope.SearchProperties, scope.SearchOwners);
      rows = rows.Where(p => ((string)(object)p.PlanNo).Contains(upper) || matchingProperties.Contains(p.PropertyId)
        || (p.ApplicantOwnerId != null && matchingOwners.Contains(p.ApplicantOwnerId)));
    }

    var total = await rows.CountAsync(cancellationToken);
    var all = await rows.ToListAsync(cancellationToken);
    var codes = await read.PropertyCodesAsync(all.Select(p => p.PropertyId), cancellationToken);
    var statusRefs = await masters.Refs().Add<BuildingPlanStatus>(all.Select(p => p.BuildingPlanStatusId)).LoadAsync(cancellationToken);

    var desc = query.SortDescending;
    var ordered = query.SortBy switch
    {
      BuildingPlanSort.Code => all.ByText(p => p.PlanNo.Value, desc),
      BuildingPlanSort.Property => all.ByText(p => codes[p.PropertyId], desc),
      BuildingPlanSort.Status => all.ByText(p => statusRefs[p.BuildingPlanStatusId]?.Name ?? string.Empty, desc),
      _ => all.NullsLast(p => p.SubmissionDate, desc),
    };
    var page = ordered.ThenBy(p => p.PlanNo.Value, StringComparer.Ordinal).ThenBy(p => p.Id.Value).Page(query.Pagination);

    var refs = await masters.Refs()
        .Add<BuildingPlanType>(page.Select(p => p.BuildingPlanTypeId))
        .Add<BuildingPlanStatus>(page.Select(p => p.BuildingPlanStatusId))
        .Add<MeasurementUnit>(page.Select(p => p.MeasurementUnitId))
        .LoadAsync(cancellationToken);
    var owners = await read.OwnerRefsAsync(page.Select(p => p.ApplicantOwnerId), cancellationToken);
    var properties = await read.PropertyRefsAsync(page.Select(p => (PropertyId?)p.PropertyId), cancellationToken);

    var data = page.Select(p => p.ToDto(refs, owners, properties.GetValueOrDefault(p.PropertyId))).ToList();
    return Result<GetAllBuildingPlansQueryResult>.Success(new GetAllBuildingPlansQueryResult(
      new PaginatedResult<BuildingPlanDto>(query.Pagination.Pageindex, query.Pagination.PageSize, total, data)));
  }
}
