public sealed record GetBuildingPlansQueryResult(IReadOnlyList<BuildingPlanDto> Plans);
public sealed record GetBuildingPlanQueryResult(BuildingPlanDto Plan);

/// Latest revision of each plan by default; includeRevisions=true returns every revision.
public sealed record GetPropertyBuildingPlansQuery(Guid PropertyId, bool IncludeRevisions = false) : IQuery<Result<GetBuildingPlansQueryResult>>;

public sealed record GetBuildingPlanQuery(Guid Id) : IQuery<Result<GetBuildingPlanQueryResult>>;
