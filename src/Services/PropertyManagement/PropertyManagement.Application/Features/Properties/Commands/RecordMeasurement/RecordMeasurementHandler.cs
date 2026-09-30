using Microsoft.EntityFrameworkCore;

public class RecordMeasurementHandler(IApplicationDbContext context, MasterLookup masters)
  : ICommandHandler<RecordMeasurementCommand, Result<RecordMeasurementCommandResult>>
{
  public async Task<Result<RecordMeasurementCommandResult>> Handle(RecordMeasurementCommand command, CancellationToken cancellationToken)
  {
    var property = await context.LoadPropertyAsync(command.PropertyId, cancellationToken);
    var input = command.Measurement;
    var unit = await masters.GetAsync<MeasurementUnit>(input.MeasurementUnitId, cancellationToken);

    var measurement = PropertyMeasurement.Record(
      PropertyMeasurementId.New(), property, unit, input.TotalArea, input.BuiltUpArea,
      input.MeasuredOn, input.MeasurementSource, input.Remarks);

    var previous = await context.PropertyMeasurements
        .Where(m => m.PropertyId == property.Id && m.IsCurrent)
        .ToListAsync(cancellationToken);

    previous.ForEach(m => m.Supersede());

    await context.PropertyMeasurements.AddAsync(measurement, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    return Result<RecordMeasurementCommandResult>.Success(new RecordMeasurementCommandResult(measurement.Id.Value));
  }
}
