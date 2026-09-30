public class CreatePropertyHandler(IApplicationDbContext context, MasterLookup masters, CodeGenerator codes)
  : ICommandHandler<CreatePropertyCommand, Result<CreatePropertyCommandResult>>
{
  public async Task<Result<CreatePropertyCommandResult>> Handle(CreatePropertyCommand command, CancellationToken cancellationToken)
  {
    var input = command.Property;
    var set = await PropertyMasters.LoadAsync(masters, input, cancellationToken);
    var status = await masters.GetAsync<PropertyStatus>(command.PropertyStatusId, cancellationToken);

    var property = Property.Create(
      PropertyId.New(),
      await codes.NextAsync(CodeSequenceKeys.Property, cancellationToken),
      Name.Of(input.PropertyName, Property.NameMaxLength),
      set.Town, set.Type, status, set.Classification,
      input.AddressLine, input.KhasraSurveyNo, input.Description, input.Remarks);

    await context.Properties.AddAsync(property, cancellationToken);

    var effectiveFrom = command.StatusEffectiveFrom ?? DateOnly.FromDateTime(DateTime.UtcNow);
    await context.PropertyStatusHistories.AddAsync(property.OpenStatusHistory(effectiveFrom, "Property registered"), cancellationToken);

    if (command.Measurement is { } measurement)
    {
      var unit = await masters.GetAsync<MeasurementUnit>(measurement.MeasurementUnitId, cancellationToken);
      await context.PropertyMeasurements.AddAsync(PropertyMeasurement.Record(
        PropertyMeasurementId.New(), property, unit, measurement.TotalArea, measurement.BuiltUpArea,
        measurement.MeasuredOn, measurement.MeasurementSource, measurement.Remarks), cancellationToken);
    }

    await context.SaveChangesAsync(cancellationToken);

    return Result<CreatePropertyCommandResult>.Success(new CreatePropertyCommandResult(property.Id.Value, property.PropertyCode.Value));
  }
}
