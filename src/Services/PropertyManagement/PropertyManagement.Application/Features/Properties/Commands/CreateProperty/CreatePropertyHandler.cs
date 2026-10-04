using Microsoft.EntityFrameworkCore;

public class CreatePropertyHandler(IApplicationDbContext context, MasterLookup masters, CodeGenerator codes)
  : ICommandHandler<CreatePropertyCommand, Result<CreatePropertyCommandResult>>
{
  public async Task<Result<CreatePropertyCommandResult>> Handle(CreatePropertyCommand command, CancellationToken cancellationToken)
  {
    var input = command.Property;
    var ownerInputs = command.Owners ?? Array.Empty<OwnershipInput>();
    var attributeInputs = command.Attributes ?? Array.Empty<AttributeValueInput>();

    var set = await PropertyMasters.LoadAsync(masters, input, cancellationToken);
    var status = await masters.GetAsync<PropertyStatus>(command.PropertyStatusId, cancellationToken);

    // ---- the parts are checked before anything is created; nothing is saved until the single SaveChanges ----

    var owners = await LoadOwnersAsync(ownerInputs, cancellationToken);

    var duplicate = ownerInputs.GroupBy(o => o.OwnerId).FirstOrDefault(g => g.Count() > 1);
    if (duplicate is not null)
    {
      var owner = owners[OwnerId.Of(duplicate.Key)];
      return Result<CreatePropertyCommandResult>.Failure(
        $"{owner.OwnerName.Value} ({owner.OwnerCode.Value}) is listed more than once. Give each owner one row with their whole share.");
    }

    var inactive = owners.Values.FirstOrDefault(o => !o.IsActive);
    if (inactive is not null)
      return Result<CreatePropertyCommandResult>.Failure(
        $"{inactive.OwnerName.Value} ({inactive.OwnerCode.Value}) is inactive and cannot be registered as an owner.");

    var attributes = await ResolveAttributesAsync(attributeInputs, cancellationToken);
    if (!attributes.IsSuccess)
      return Result<CreatePropertyCommandResult>.Failure(attributes.Message!);

    // ---- the property ----

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

    // ---- first owners: rule 1, the shares may total at most 100% ----

    var ownerships = new List<PropertyOwnership>();
    foreach (var ownerInput in ownerInputs)
    {
      var tenure = await masters.GetAsync<TenureType>(ownerInput.TenureTypeId, cancellationToken);
      var acquiredBy = await masters.GetOptionalAsync<TransferType>(ownerInput.AcquisitionTransferTypeId, cancellationToken);

      OwnershipShares.EnsureRoomFor(ownerships, ownerInput.OwnershipSharePct);

      ownerships.Add(PropertyOwnership.Register(
        OwnershipId.New(), property, owners[OwnerId.Of(ownerInput.OwnerId)], tenure, ownerInput.OwnershipSharePct,
        ownerInput.EffectiveFrom, acquiredBy, ownerInput.ReferenceNo, ownerInput.Remarks));
    }

    await context.Ownerships.AddRangeAsync(ownerships, cancellationToken);

    // ---- boundary survey ----

    if (command.Boundary is { } boundary)
      await context.Boundaries.AddAsync(PropertyBoundary.Record(
        BoundaryId.New(), property, boundary.BoundaryType, boundary.SurveyDate, boundary.SurveySource, boundary.SlopePercentage,
        boundary.Remarks, boundary.Points.ToGeoPoints()), cancellationToken);

    // ---- custom fields: each value is checked against its field's data type by the domain ----

    foreach (var (definition, value) in attributes.Value!)
      await context.PropertyAttributeValues.AddAsync(
        PropertyAttributeValue.Create(PropertyAttributeValueId.New(), property, definition, value), cancellationToken);

    await context.SaveChangesAsync(cancellationToken);

    return Result<CreatePropertyCommandResult>.Success(new CreatePropertyCommandResult(property.Id.Value, property.PropertyCode.Value));
  }

  private async Task<Dictionary<OwnerId, PropertyOwner>> LoadOwnersAsync(IReadOnlyList<OwnershipInput> inputs, CancellationToken cancellationToken)
  {
    var ids = inputs.Select(o => OwnerId.Of(o.OwnerId)).Distinct().ToList();
    if (ids.Count == 0)
      return new Dictionary<OwnerId, PropertyOwner>();

    var owners = await context.Owners.AsNoTracking().Where(o => ids.Contains(o.Id)).ToDictionaryAsync(o => o.Id, cancellationToken);

    var missing = ids.FirstOrDefault(id => !owners.ContainsKey(id));
    if (missing is not null)
      throw new OwnerNotFoundException($"Owner {missing.Value} was not found.");

    return owners;
  }

  /// The custom-field values to store: every value sent (blank values of optional fields are skipped) plus
  /// every active required field, which must end up with a value — the one sent, else its default. A required
  /// field with neither refuses the registration, naming the field.
  private async Task<Result<List<(AttributeDefinition Definition, string? Value)>>> ResolveAttributesAsync(
      IReadOnlyList<AttributeValueInput> inputs, CancellationToken cancellationToken)
  {
    var givenIds = inputs.Select(a => AttributeDefinitionId.Of(a.AttributeDefinitionId)).ToList();
    var definitions = await context.AttributeDefinitions.AsNoTracking()
        .Where(d => d.IsActive || givenIds.Contains(d.Id))
        .ToDictionaryAsync(d => d.Id, cancellationToken);

    var missing = givenIds.FirstOrDefault(id => !definitions.ContainsKey(id));
    if (missing is not null)
      throw new AttributeDefinitionNotFoundException($"Custom field {missing.Value} was not found.");

    var repeated = givenIds.GroupBy(id => id).FirstOrDefault(g => g.Count() > 1);
    if (repeated is not null)
      return Result<List<(AttributeDefinition, string?)>>.Failure($"'{definitions[repeated.Key].Label.Value}' is given more than once.");

    var given = inputs.ToDictionary(a => AttributeDefinitionId.Of(a.AttributeDefinitionId), a => a.Value);
    var values = new List<(AttributeDefinition, string?)>();

    foreach (var (id, value) in given)
      if (!string.IsNullOrWhiteSpace(value) || definitions[id].IsRequired)
        values.Add((definitions[id], value));

    foreach (var required in definitions.Values.Where(d => d.IsActive && d.IsRequired).OrderBy(d => d.DisplayOrder).ThenBy(d => d.Code.Value))
    {
      var value = given.GetValueOrDefault(required.Id);
      if (!string.IsNullOrWhiteSpace(value))
        continue;

      if (string.IsNullOrWhiteSpace(required.DefaultValue))
        return Result<List<(AttributeDefinition, string?)>>.Failure(
          $"'{required.Label.Value}' ({required.Code.Value}) is a required custom field. Give it a value to register the property.");

      // the default stands in for the missing value
      values.RemoveAll(v => v.Item1.Id == required.Id);
      values.Add((required, required.DefaultValue));
    }

    return Result<List<(AttributeDefinition, string?)>>.Success(values);
  }
}
