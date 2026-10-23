using Microsoft.EntityFrameworkCore;

public class SaveAttributeDefinitionHandler(IApplicationDbContext context, MasterLookup masters)
  : ICommandHandler<SaveAttributeDefinitionCommand, Result<SaveAttributeDefinitionCommandResult>>
{
  public async Task<Result<SaveAttributeDefinitionCommandResult>> Handle(SaveAttributeDefinitionCommand command, CancellationToken cancellationToken)
  {
    var input = command.Definition;
    var group = await masters.GetAsync<AttributeGroup>(input.AttributeGroupId, cancellationToken);
    var label = Name.Of(input.Label, 100);
    await EnsureScopeExistsAsync(input, cancellationToken);

    if (command.Id is { } id)
    {
      var definition = await context.LoadAttributeDefinitionAsync(id, cancellationToken);
      var inUse = await context.PropertyAttributeValues.IgnoreQueryFilters().AsNoTracking()
          .Where(v => v.AttributeDefinitionId == definition.Id && v.Value != null)
          .Select(v => v.Value!)
          .ToListAsync(cancellationToken);

      definition.Update(group, label, input.DataType, input.IsRequired, input.OptionsCsv, input.DefaultValue, input.DisplayOrder, inUse);
      definition.SetScope(input.AppliesToPropertyTypeIds, input.AppliesToClassificationIds);

      await context.SaveChangesAsync(cancellationToken);
      return Result<SaveAttributeDefinitionCommandResult>.Success(new SaveAttributeDefinitionCommandResult(definition.Id.Value, definition.Code.Value));
    }

    var taken = (await context.AttributeDefinitions.AsNoTracking().Select(d => d.Code).ToListAsync(cancellationToken))
      .Select(c => c.Value).ToHashSet();

    // a code given by hand is kept (and must be free); otherwise one is made from the label
    MasterCode code;
    if (!string.IsNullOrWhiteSpace(input.Code))
    {
      code = MasterCode.Of(input.Code);
      if (taken.Contains(code.Value))
        return Result<SaveAttributeDefinitionCommandResult>.Failure($"Another field already uses the key {code.Value}; leave the key empty and one is made from the name.");
    }
    else
    {
      code = MasterCode.Of(AttributeCodes.FromLabel(label.Value, taken));
    }

    var created = AttributeDefinition.Create(AttributeDefinitionId.New(), group, code, label, input.DataType,
      input.IsRequired, input.OptionsCsv, input.DefaultValue, input.DisplayOrder);
    created.SetScope(input.AppliesToPropertyTypeIds, input.AppliesToClassificationIds);

    await context.AttributeDefinitions.AddAsync(created, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    return Result<SaveAttributeDefinitionCommandResult>.Success(new SaveAttributeDefinitionCommandResult(created.Id.Value, created.Code.Value));
  }

  private async Task EnsureScopeExistsAsync(AttributeDefinitionInput input, CancellationToken cancellationToken)
  {
    foreach (var typeId in input.AppliesToPropertyTypeIds ?? Array.Empty<Guid>())
      await masters.GetAsync<PropertyType>(typeId, cancellationToken);
    foreach (var classificationId in input.AppliesToClassificationIds ?? Array.Empty<Guid>())
      await masters.GetAsync<PropertyClassification>(classificationId, cancellationToken);
  }
}
