using Microsoft.EntityFrameworkCore;

public class HideStructureFieldHandler(IApplicationDbContext context, IAttributeSchemaService schemaService)
  : ICommandHandler<HideStructureFieldCommand, Result<HideStructureFieldCommandResult>>,
    ICommandHandler<UnhideStructureFieldCommand, Result<HideStructureFieldCommandResult>>
{
  public async Task<Result<HideStructureFieldCommandResult>> Handle(HideStructureFieldCommand command, CancellationToken cancellationToken)
  {
    var target = command.Scope == AttributeScope.AssetType
      ? AttributeScopeTarget.Of(command.Scope, null, command.TargetId, null, null)
      : AttributeScopeTarget.Of(command.Scope, null, null, command.TargetId, null);
    await AttributeAssignmentTargets.EnsureExistsAsync(context, target, cancellationToken);

    var definitionId = AttributeDefinitionId.Of(command.DefinitionId);
    var definition = await context.AttributeDefinitions.AsNoTracking().FirstOrDefaultAsync(d => d.Id == definitionId, cancellationToken)
      ?? throw new AttributeDefinitionNotFoundException("That field was not found.");

    var own = await context.AttributeAssignments.FirstOrDefaultAsync(a =>
        a.AttributeDefinitionId == definitionId
        && a.AssetClassId == target.AssetClassId && a.AssetTypeId == target.AssetTypeId && a.CategoryId == target.CategoryId && a.AssetId == null,
      cancellationToken);
    if (own is { IsHidden: true })
      return Result<HideStructureFieldCommandResult>.Failure($"'{definition.Name.Value}' is already left off this form.");
    if (own is not null)
      return Result<HideStructureFieldCommandResult>.Failure($"'{definition.Name.Value}' is added on this node itself. Remove it here instead.");

    var hidden = AttributeAssignment.CreateHidden(AttributeAssignmentId.Of(Guid.NewGuid()), definitionId, target, command.InheritToChildren);
    await context.AttributeAssignments.AddAsync(hidden, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    await schemaService.ReprojectAsync(await AssignmentReach.AssetsUnderAsync(context, hidden, cancellationToken), cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    return Result<HideStructureFieldCommandResult>.Success(new HideStructureFieldCommandResult(hidden.Id.Value));
  }

  public async Task<Result<HideStructureFieldCommandResult>> Handle(UnhideStructureFieldCommand command, CancellationToken cancellationToken)
  {
    var id = AttributeAssignmentId.Of(command.AssignmentId);
    var hidden = await context.AttributeAssignments.FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
      ?? throw new AttributeAssignmentNotFoundException("That field is already used here.");

    if (!hidden.IsHidden)
      return Result<HideStructureFieldCommandResult>.Failure("That field is used here; there is nothing to undo.");

    context.AttributeAssignments.Remove(hidden);
    await context.SaveChangesAsync(cancellationToken);

    await schemaService.ReprojectAsync(await AssignmentReach.AssetsUnderAsync(context, hidden, cancellationToken), cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    return Result<HideStructureFieldCommandResult>.Success(new HideStructureFieldCommandResult(hidden.Id.Value));
  }
}
