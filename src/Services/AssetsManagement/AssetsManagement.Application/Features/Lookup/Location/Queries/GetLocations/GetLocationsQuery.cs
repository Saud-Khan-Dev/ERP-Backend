public sealed record GetLocationsQueryResult(IReadOnlyList<LocationDto> Locations);

public sealed record GetLocationsQuery(Guid? ParentLocationId, bool OnlyRoots, string? LocationType, bool IncludeInactive)
  : IQuery<Result<GetLocationsQueryResult>>;
