using Microsoft.EntityFrameworkCore;

public class CreateAttributeDefinitionHandler(IApplicationDbContext context)
  : ICommandHandler<CreateAttributeDefinitionCommand, Result<CreateAttributeDefinitionCommandResult>>
{
  public async Task<Result<CreateAttributeDefinitionCommandResult>> Handle(CreateAttributeDefinitionCommand command, CancellationToken cancellationToken)
  {
    var input = command.Definition;
    var code = AttributeCode.Of(input.Code);

    if (await context.AttributeDefinitions.AnyAsync(d => d.Code == code, cancellationToken))
      return Result<CreateAttributeDefinitionCommandResult>.Failure($"An attribute with code {code.Value} already exists.");

    OptionSetId? optionSetId = null;
    if (input.OptionSetId.HasValue)
    {
      optionSetId = OptionSetId.Of(input.OptionSetId.Value);
      if (!await context.OptionSets.AnyAsync(o => o.Id == optionSetId, cancellationToken))
        throw new OptionSetNotFoundException($"Option set {input.OptionSetId} was not found.");
    }

    var definition = AttributeDefinition.Create(
      id: AttributeDefinitionId.Of(Guid.NewGuid()),
      code: code,
      name: Name.Of(input.Name, 150),
      description: input.Description,
      dataType: input.DataType,
      optionSetId: optionSetId,
      referenceEntity: input.ReferenceEntity,
      unit: input.Unit,
      numericPrecision: input.NumericPrecision,
      numericScale: input.NumericScale,
      rules: input.ToRules(),
      isMultiValue: input.IsMultiValue,
      isPii: input.IsPii,
      isSystem: input.IsSystem);

    if (!input.IsActive)
      definition.Deactivate();

    await context.AttributeDefinitions.AddAsync(definition, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    return Result<CreateAttributeDefinitionCommandResult>.Success(new CreateAttributeDefinitionCommandResult(definition.Id.Value));
  }
}
