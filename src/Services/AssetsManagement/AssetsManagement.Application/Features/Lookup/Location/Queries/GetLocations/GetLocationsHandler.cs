using Microsoft.EntityFrameworkCore;

public class GetLocationsHandler(IApplicationDbContext context)
  : IQueryHandler<GetLocationsQuery, Result<GetLocationsQueryResult>>
{
  public async Task<Result<GetLocationsQueryResult>> Handle(GetLocationsQuery query, CancellationToken cancellationToken)
  {
    var locations = context.Locations.AsNoTracking();

    if (query.ParentLocationId.HasValue)
    {
      var parentId = LocationId.Of(query.ParentLocationId.Value);
      locations = locations.Where(l => l.ParentLocationId == parentId);
    }
    else if (query.OnlyRoots)
    {
      locations = locations.Where(l => l.ParentLocationId == null);
    }

    if (!string.IsNullOrWhiteSpace(query.LocationType))
    {
      var type = query.LocationType.Trim().ToUpperInvariant();
      locations = locations.Where(l => l.LocationType == type);
    }

    if (!query.IncludeInactive)
      locations = locations.Where(l => l.IsActive);

    var data = await locations.OrderBy(l => l.Code).ToListAsync(cancellationToken);
    var ordered = data.OrderBy(l => l.Path, StringComparer.Ordinal).Select(l => l.ToDto()).ToList();

    return Result<GetLocationsQueryResult>.Success(new GetLocationsQueryResult(ordered));
  }
}
