using Microsoft.EntityFrameworkCore;

public class DeleteAttributeDefinitionHandler(IApplicationDbContext context, IAssetDataQueries queries)
  : ICommandHandler<DeleteAttributeDefinitionCommand, Result<DeleteAttributeDefinitionCommandResult>>
{
  public async Task<Result<DeleteAttributeDefinitionCommandResult>> Handle(DeleteAttributeDefinitionCommand command, CancellationToken cancellationToken)
  {
    var id = AttributeDefinitionId.Of(command.Id);
    var definition = await context.AttributeDefinitions.FirstOrDefaultAsync(d => d.Id == id, cancellationToken)
      ?? throw new AttributeDefinitionNotFoundException("That field was not found.");

    definition.EnsureDeletable();

    // only values that assets (not deleted ones) hold count - the "asset created" lines of its history do not
    var holders = (await queries.AssetsHoldingAsync([definition.Code.Value], cancellationToken)).GetValueOrDefault(definition.Code.Value);
    if (holders > 0)
      return Result<DeleteAttributeDefinitionCommandResult>.Failure(
        $"{holders} {(holders == 1 ? "asset holds" : "assets hold")} a value for '{definition.Name.Value}', so it cannot be deleted. Switch it off instead - the values stay on their records.");

    // its places on forms, its typed rows and its history go with it (cascade); a list of choices only it used goes too
    var optionSetId = definition.OptionSetId;
    context.AttributeDefinitions.Remove(definition);
    await context.SaveChangesAsync(cancellationToken);

    if (optionSetId is not null && !await context.AttributeDefinitions.AnyAsync(d => d.OptionSetId == optionSetId, cancellationToken))
    {
      var optionSet = await context.OptionSets.Include(o => o.Values).FirstOrDefaultAsync(o => o.Id == optionSetId, cancellationToken);
      if (optionSet is { IsSystem: false })
      {
        context.OptionSets.Remove(optionSet);
        await context.SaveChangesAsync(cancellationToken);
      }
    }

    return Result<DeleteAttributeDefinitionCommandResult>.Success(new DeleteAttributeDefinitionCommandResult(true));
  }
}
