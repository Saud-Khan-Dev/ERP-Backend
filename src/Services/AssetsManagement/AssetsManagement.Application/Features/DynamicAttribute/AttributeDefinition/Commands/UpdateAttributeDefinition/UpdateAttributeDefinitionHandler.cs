using Microsoft.EntityFrameworkCore;

public class UpdateAttributeDefinitionHandler(IApplicationDbContext context)
  : ICommandHandler<UpdateAttributeDefinitionCommand, Result<UpdateAttributeDefinitionCommandResult>>
{
  public async Task<Result<UpdateAttributeDefinitionCommandResult>> Handle(UpdateAttributeDefinitionCommand command, CancellationToken cancellationToken)
  {
    var id = AttributeDefinitionId.Of(command.Id);
    var definition = await context.AttributeDefinitions.FirstOrDefaultAsync(d => d.Id == id, cancellationToken)
      ?? throw new AttributeDefinitionNotFoundException($"Attribute definition {command.Id} was not found.");

    var input = command.Definition;
    var code = AttributeCode.Of(input.Code);
    var hasValues = await context.AssetAttributeValues.AnyAsync(v => v.AttributeDefinitionId == id, cancellationToken)
                    || await context.AssetAttributeHistories.AnyAsync(h => h.AttributeDefinitionId == id, cancellationToken);

    if (definition.Code != code)
    {
      // the code is the JSONB key inside asset.extra_attributes: immutable once assets hold values
      if (hasValues)
        return Result<UpdateAttributeDefinitionCommandResult>.Failure($"Attribute code '{definition.Code.Value}' cannot be changed because assets already hold values for it.");

      if (await context.AttributeDefinitions.AnyAsync(d => d.Id != id && d.Code == code, cancellationToken))
        return Result<UpdateAttributeDefinitionCommandResult>.Failure($"An attribute with code {code.Value} already exists.");

      definition.Rename(code);
    }

    if (hasValues && definition.DataType != input.DataType)
      return Result<UpdateAttributeDefinitionCommandResult>.Failure("The data type cannot be changed because assets already hold values for this attribute.");

    OptionSetId? optionSetId = null;
    if (input.OptionSetId.HasValue)
    {
      optionSetId = OptionSetId.Of(input.OptionSetId.Value);
      if (!await context.OptionSets.AnyAsync(o => o.Id == optionSetId, cancellationToken))
        throw new OptionSetNotFoundException($"Option set {input.OptionSetId} was not found.");
    }

    definition.Update(
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
      isActive: input.IsActive);

    await context.SaveChangesAsync(cancellationToken);

    return Result<UpdateAttributeDefinitionCommandResult>.Success(new UpdateAttributeDefinitionCommandResult(true));
  }
}
