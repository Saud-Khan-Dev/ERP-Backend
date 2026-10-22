using Microsoft.EntityFrameworkCore;

public sealed record DeleteAttributeDefinitionCommandResult(bool IsSuccess);

/// Deletes a field no property holds a value for (made by mistake, a leftover). One with values is switched off instead,
/// so the values stay readable.
public sealed record DeleteAttributeDefinitionCommand(Guid Id) : ICommand<Result<DeleteAttributeDefinitionCommandResult>>;

public class DeleteAttributeDefinitionHandler(IApplicationDbContext context)
  : ICommandHandler<DeleteAttributeDefinitionCommand, Result<DeleteAttributeDefinitionCommandResult>>
{
  public async Task<Result<DeleteAttributeDefinitionCommandResult>> Handle(DeleteAttributeDefinitionCommand command, CancellationToken cancellationToken)
  {
    var definition = await context.LoadAttributeDefinitionAsync(command.Id, cancellationToken);
    var values = await context.PropertyAttributeValues.IgnoreQueryFilters().Where(v => v.AttributeDefinitionId == definition.Id).ToListAsync(cancellationToken);
    var held = values.Count(v => v.Value != null);

    if (held > 0)
      return Result<DeleteAttributeDefinitionCommandResult>.Failure(
        $"{held} propert{(held == 1 ? "y holds" : "ies hold")} a value for '{definition.Label.Value}', so it cannot be deleted. Switch it off instead: the values stay readable.");

    // rows a property saved empty carry nothing
    foreach (var empty in values)
      context.PropertyAttributeValues.Remove(empty);

    context.AttributeDefinitions.Remove(definition);
    await context.SaveChangesAsync(cancellationToken);
    return Result<DeleteAttributeDefinitionCommandResult>.Success(new DeleteAttributeDefinitionCommandResult(true));
  }
}
