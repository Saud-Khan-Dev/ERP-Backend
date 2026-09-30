using Microsoft.EntityFrameworkCore;

public class GetMeasurementsHandler(IApplicationDbContext context, MasterLookup masters)
  : IQueryHandler<GetMeasurementsQuery, Result<GetMeasurementsQueryResult>>
{
  public async Task<Result<GetMeasurementsQueryResult>> Handle(GetMeasurementsQuery query, CancellationToken cancellationToken)
  {
    var propertyId = PropertyId.Of(query.PropertyId);

    if (!await context.Properties.AnyAsync(p => p.Id == propertyId, cancellationToken))
      throw new PropertyNotFoundException($"Property {query.PropertyId} was not found.");

    var measurements = await context.PropertyMeasurements.AsNoTracking()
        .Where(m => m.PropertyId == propertyId)
        .OrderByDescending(m => m.IsCurrent).ThenByDescending(m => m.CreatedAt)
        .ToListAsync(cancellationToken);

    var refs = await masters.Refs().Add<MeasurementUnit>(measurements.Select(m => m.MeasurementUnitId)).LoadAsync(cancellationToken);

    return Result<GetMeasurementsQueryResult>.Success(
      new GetMeasurementsQueryResult(measurements.Select(m => m.ToDto(refs)).ToList()));
  }
}
