using Microsoft.EntityFrameworkCore;

public class DeleteAttributeDefinitionHandler(IApplicationDbContext context)
  : ICommandHandler<DeleteAttributeDefinitionCommand, Result<DeleteAttributeDefinitionCommandResult>>
{
  public async Task<Result<DeleteAttributeDefinitionCommandResult>> Handle(DeleteAttributeDefinitionCommand command, CancellationToken cancellationToken)
  {
    var id = AttributeDefinitionId.Of(command.Id);
    var definition = await context.AttributeDefinitions.FirstOrDefaultAsync(d => d.Id == id, cancellationToken)
      ?? throw new AttributeDefinitionNotFoundException($"Attribute definition {command.Id} was not found.");

    definition.EnsureDeletable();

    var capturedValues = await context.AssetAttributeValues.CountAsync(v => v.AttributeDefinitionId == id, cancellationToken)
                         + await context.AssetAttributeHistories.CountAsync(h => h.AttributeDefinitionId == id, cancellationToken);

    if (capturedValues > 0)
      return Result<DeleteAttributeDefinitionCommandResult>.Failure(
        $"This attribute has {capturedValues} recorded value(s) and cannot be deleted. Deactivate it instead.");

    // assignments cascade in the DB
    context.AttributeDefinitions.Remove(definition);
    await context.SaveChangesAsync(cancellationToken);

    return Result<DeleteAttributeDefinitionCommandResult>.Success(new DeleteAttributeDefinitionCommandResult(true));
  }
}
