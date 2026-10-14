using Microsoft.EntityFrameworkCore;

public class RecordMeasurementHandler(IApplicationDbContext context, MasterLookup masters)
  : ICommandHandler<RecordMeasurementCommand, Result<RecordMeasurementCommandResult>>
{
  public async Task<Result<RecordMeasurementCommandResult>> Handle(RecordMeasurementCommand command, CancellationToken cancellationToken)
  {
    var property = await context.LoadPropertyAsync(command.PropertyId, cancellationToken);
    var input = command.Measurement;
    var unit = await masters.GetAsync<MeasurementUnit>(input.MeasurementUnitId, cancellationToken);

    var current = await context.PropertyMeasurements
        .Where(m => m.PropertyId == property.Id && m.IsCurrent)
        .ToListAsync(cancellationToken);

    // the domain decides whether this survey is the latest (supersedes the current one) or history
    var measurement = PropertyMeasurement.Record(
      PropertyMeasurementId.New(), property, unit, input.TotalArea, input.BuiltUpArea,
      input.MeasuredOn, input.MeasurementSource, input.Remarks, PropertyWriteGuard.Today(), current, out var warnings);

    await context.PropertyMeasurements.AddAsync(measurement, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    return Result<RecordMeasurementCommandResult>.Success(new RecordMeasurementCommandResult(measurement.Id.Value, measurement.IsCurrent, warnings));
  }
}
