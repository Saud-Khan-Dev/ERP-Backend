using Microsoft.EntityFrameworkCore;

public class DeleteAttributeGroupHandler(IApplicationDbContext context)
  : ICommandHandler<DeleteAttributeGroupCommand, Result<DeleteAttributeGroupCommandResult>>
{
  public async Task<Result<DeleteAttributeGroupCommandResult>> Handle(DeleteAttributeGroupCommand command, CancellationToken cancellationToken)
  {
    var id = AttributeGroupId.Of(command.Id);
    var group = await context.AttributeGroups.FirstOrDefaultAsync(g => g.Id == id, cancellationToken)
      ?? throw new AttributeGroupNotFoundException($"Attribute group {command.Id} was not found.");

    // assignments keep working: their attribute_group_id is set to NULL by the FK
    context.AttributeGroups.Remove(group);
    await context.SaveChangesAsync(cancellationToken);

    return Result<DeleteAttributeGroupCommandResult>.Success(new DeleteAttributeGroupCommandResult(true));
  }
}
