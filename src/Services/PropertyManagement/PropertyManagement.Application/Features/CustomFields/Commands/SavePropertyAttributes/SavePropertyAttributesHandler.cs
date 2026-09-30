using Microsoft.EntityFrameworkCore;

public class SavePropertyAttributesHandler(IApplicationDbContext context)
  : ICommandHandler<SavePropertyAttributesCommand, Result<SavePropertyAttributesCommandResult>>
{
  public async Task<Result<SavePropertyAttributesCommandResult>> Handle(SavePropertyAttributesCommand command, CancellationToken cancellationToken)
  {
    var property = await context.LoadPropertyAsync(command.PropertyId, cancellationToken);

    var definitionIds = command.Values.Select(v => AttributeDefinitionId.Of(v.AttributeDefinitionId)).Distinct().ToList();
    var definitions = await context.AttributeDefinitions.Where(d => definitionIds.Contains(d.Id)).ToListAsync(cancellationToken);
    var existing = await context.PropertyAttributeValues
        .Where(v => v.PropertyId == property.Id && definitionIds.Contains(v.AttributeDefinitionId))
        .ToListAsync(cancellationToken);

    foreach (var input in command.Values)
    {
      var definitionId = AttributeDefinitionId.Of(input.AttributeDefinitionId);
      var definition = definitions.FirstOrDefault(d => d.Id == definitionId)
        ?? throw new AttributeDefinitionNotFoundException($"Custom field {input.AttributeDefinitionId} was not found.");

      var value = existing.FirstOrDefault(v => v.AttributeDefinitionId == definitionId);

      if (value is null)
      {
        value = PropertyAttributeValue.Create(PropertyAttributeValueId.New(), property, definition, input.Value);
        existing.Add(value);
        await context.PropertyAttributeValues.AddAsync(value, cancellationToken);
      }
      else
      {
        value.UpdateValue(definition, input.Value);
      }
    }

    await context.SaveChangesAsync(cancellationToken);
    return Result<SavePropertyAttributesCommandResult>.Success(new SavePropertyAttributesCommandResult(command.Values.Count));
  }
}
