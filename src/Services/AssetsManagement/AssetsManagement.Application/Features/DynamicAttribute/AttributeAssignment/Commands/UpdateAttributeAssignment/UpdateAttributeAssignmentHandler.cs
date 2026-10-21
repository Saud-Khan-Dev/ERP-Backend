using Microsoft.EntityFrameworkCore;

public class UpdateAttributeAssignmentHandler(IApplicationDbContext context, IAttributeSchemaService schemaService)
  : ICommandHandler<UpdateAttributeAssignmentCommand, Result<UpdateAttributeAssignmentCommandResult>>
{
  public async Task<Result<UpdateAttributeAssignmentCommandResult>> Handle(UpdateAttributeAssignmentCommand command, CancellationToken cancellationToken)
  {
    var id = AttributeAssignmentId.Of(command.Id);
    var assignment = await context.AttributeAssignments.FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
      ?? throw new AttributeAssignmentNotFoundException("That field is no longer on this form.");

    AttributeGroupId? groupId = null;
    if (command.AttributeGroupId.HasValue)
    {
      groupId = AttributeGroupId.Of(command.AttributeGroupId.Value);
      if (!await context.AttributeGroups.AnyAsync(g => g.Id == groupId, cancellationToken))
        throw new AttributeGroupNotFoundException("The form section was not found. Choose it again.");
    }

    AttributeAssignmentId? dependsOn = null;
    if (command.DependsOnAssignmentId.HasValue)
    {
      dependsOn = AttributeAssignmentId.Of(command.DependsOnAssignmentId.Value);
      if (!await context.AttributeAssignments.AnyAsync(a => a.Id == dependsOn, cancellationToken))
        throw new AttributeAssignmentNotFoundException("The field it depends on is no longer on the form.");
    }

    var wasProjected = assignment.IsProjected;
    var wasActive = assignment.IsActive;
    var wasInherited = assignment.InheritToChildren;

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

    // "Filter" or "Search" switched, or the field put on or off the form: the values filters and the search box read
    // are rebuilt for the assets under the node, so existing assets are found without being saved again
    if (wasProjected != assignment.IsProjected || wasActive != assignment.IsActive || wasInherited != assignment.InheritToChildren)
    {
      var assets = await AssignmentReach.AssetsUnderAsync(context, assignment, cancellationToken);
      await schemaService.ReprojectAsync(assets, cancellationToken);
      await context.SaveChangesAsync(cancellationToken);
    }

    return Result<UpdateAttributeAssignmentCommandResult>.Success(new UpdateAttributeAssignmentCommandResult(true));
  }
}
