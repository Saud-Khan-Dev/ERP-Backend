using Microsoft.EntityFrameworkCore;

public class UpdateAttributeAssignmentHandler(IApplicationDbContext context)
  : ICommandHandler<UpdateAttributeAssignmentCommand, Result<UpdateAttributeAssignmentCommandResult>>
{
  public async Task<Result<UpdateAttributeAssignmentCommandResult>> Handle(UpdateAttributeAssignmentCommand command, CancellationToken cancellationToken)
  {
    var id = AttributeAssignmentId.Of(command.Id);
    var assignment = await context.AttributeAssignments.FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
      ?? throw new AttributeAssignmentNotFoundException($"Attribute assignment {command.Id} was not found.");

    AttributeGroupId? groupId = null;
    if (command.AttributeGroupId.HasValue)
    {
      groupId = AttributeGroupId.Of(command.AttributeGroupId.Value);
      if (!await context.AttributeGroups.AnyAsync(g => g.Id == groupId, cancellationToken))
        throw new AttributeGroupNotFoundException($"Attribute group {command.AttributeGroupId} was not found.");
    }

    AttributeAssignmentId? dependsOn = null;
    if (command.DependsOnAssignmentId.HasValue)
    {
      dependsOn = AttributeAssignmentId.Of(command.DependsOnAssignmentId.Value);
      if (!await context.AttributeAssignments.AnyAsync(a => a.Id == dependsOn, cancellationToken))
        throw new AttributeAssignmentNotFoundException($"Parent assignment {command.DependsOnAssignmentId} was not found.");
    }

    assignment.Update(groupId, new AttributeAssignmentOptions(
      command.LabelOverride,
      command.IsRequired,
      command.IsReadonly,
      command.IsSearchable,
      command.IsFilterable,
      command.IsVisibleInList,
      command.InheritToChildren,
      command.DefaultValue,
      command.DisplayOrder,
      dependsOn,
      command.DependsOnValue), command.IsActive);

    await context.SaveChangesAsync(cancellationToken);

    return Result<UpdateAttributeAssignmentCommandResult>.Success(new UpdateAttributeAssignmentCommandResult(true));
  }
}
