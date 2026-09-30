using Microsoft.EntityFrameworkCore;

public class SaveAttributeDefinitionHandler(IApplicationDbContext context, MasterLookup masters)
  : ICommandHandler<SaveAttributeDefinitionCommand, Result<SaveAttributeDefinitionCommandResult>>
{
  public async Task<Result<SaveAttributeDefinitionCommandResult>> Handle(SaveAttributeDefinitionCommand command, CancellationToken cancellationToken)
  {
    var input = command.Definition;
    var group = await masters.GetAsync<AttributeGroup>(input.AttributeGroupId, cancellationToken);
    var label = Name.Of(input.Label, 100);

    if (command.Id is { } id)
    {
      var definition = await context.LoadAttributeDefinitionAsync(id, cancellationToken);
      definition.Update(group, label, input.DataType, input.IsRequired, input.OptionsCsv, input.DefaultValue, input.DisplayOrder);

      await context.SaveChangesAsync(cancellationToken);
      return Result<SaveAttributeDefinitionCommandResult>.Success(new SaveAttributeDefinitionCommandResult(definition.Id.Value));
    }

    var code = MasterCode.Of(input.Code);
    if (await context.AttributeDefinitions.AnyAsync(d => d.Code == code, cancellationToken))
      return Result<SaveAttributeDefinitionCommandResult>.Failure($"A custom field with code {code.Value} already exists.");

    var created = AttributeDefinition.Create(AttributeDefinitionId.New(), group, code, label, input.DataType,
      input.IsRequired, input.OptionsCsv, input.DefaultValue, input.DisplayOrder);

    await context.AttributeDefinitions.AddAsync(created, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    return Result<SaveAttributeDefinitionCommandResult>.Success(new SaveAttributeDefinitionCommandResult(created.Id.Value));
  }
}
