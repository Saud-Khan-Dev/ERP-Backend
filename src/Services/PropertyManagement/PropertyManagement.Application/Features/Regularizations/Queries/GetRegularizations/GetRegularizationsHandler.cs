using Microsoft.EntityFrameworkCore;

public class GetRegularizationsHandler(IApplicationDbContext context, MasterLookup masters)
  : IQueryHandler<GetRegularizationsQuery, Result<GetRegularizationsQueryResult>>
{
  public async Task<Result<GetRegularizationsQueryResult>> Handle(GetRegularizationsQuery query, CancellationToken cancellationToken)
  {
    var propertyId = PropertyId.Of(query.PropertyId);

    if (!await context.Properties.AnyAsync(p => p.Id == propertyId, cancellationToken))
      throw new PropertyNotFoundException($"Property {query.PropertyId} was not found.");

    var cases = await context.AreaRegularizations.AsNoTracking()
        .Where(r => r.PropertyId == propertyId && (query.IncludeInactive || r.IsActive))
        .OrderByDescending(r => r.CreatedAt)
        .ToListAsync(cancellationToken);

    var refs = await masters.Refs().Add<MeasurementUnit>(cases.Select(r => r.MeasurementUnitId)).LoadAsync(cancellationToken);
    var today = DateOnly.FromDateTime(DateTime.UtcNow);

    return Result<GetRegularizationsQueryResult>.Success(
      new GetRegularizationsQueryResult(cases.Select(r => r.ToDto(refs, today)).ToList()));
  }
}
