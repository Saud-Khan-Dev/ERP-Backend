using Microsoft.EntityFrameworkCore;

public class UpdateAttributeDefinitionHandler(IApplicationDbContext context, IAssetDataQueries queries)
  : ICommandHandler<UpdateAttributeDefinitionCommand, Result<UpdateAttributeDefinitionCommandResult>>
{
  public async Task<Result<UpdateAttributeDefinitionCommandResult>> Handle(UpdateAttributeDefinitionCommand command, CancellationToken cancellationToken)
  {
    var id = AttributeDefinitionId.Of(command.Id);
    var definition = await context.AttributeDefinitions.FirstOrDefaultAsync(d => d.Id == id, cancellationToken)
      ?? throw new AttributeDefinitionNotFoundException("That field was not found.");

    var input = command.Definition;
    var code = AttributeCode.Of(input.Code);
    // values are kept in the assets' own details (deleted assets included), not only in the filter rows
    var hasValues = (await queries.AssetsHoldingAsync([definition.Code.Value], cancellationToken)).Count > 0
                    || await context.AssetAttributeHistories.AnyAsync(h => h.AttributeDefinitionId == id, cancellationToken);

    if (definition.Code != code)
    {
      // the code is the JSONB key inside asset.extra_attributes: immutable once assets hold values
      if (hasValues)
        return Result<UpdateAttributeDefinitionCommandResult>.Failure($"The code of '{definition.Name.Value}' cannot change: assets already hold values for it.");

      if (await context.AttributeDefinitions.AnyAsync(d => d.Id != id && d.Code == code, cancellationToken))
        return Result<UpdateAttributeDefinitionCommandResult>.Failure("Another field already uses this code.");

      definition.Rename(code);
    }

    if (hasValues && definition.DataType != input.DataType)
      return Result<UpdateAttributeDefinitionCommandResult>.Failure($"What '{definition.Name.Value}' takes (text, number, date …) cannot change: assets already hold values for it.");

    OptionSetId? optionSetId = null;
    if (input.OptionSetId.HasValue)
    {
      optionSetId = OptionSetId.Of(input.OptionSetId.Value);
      if (!await context.OptionSets.AnyAsync(o => o.Id == optionSetId, cancellationToken))
        throw new OptionSetNotFoundException("The list of choices was not found.");
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
