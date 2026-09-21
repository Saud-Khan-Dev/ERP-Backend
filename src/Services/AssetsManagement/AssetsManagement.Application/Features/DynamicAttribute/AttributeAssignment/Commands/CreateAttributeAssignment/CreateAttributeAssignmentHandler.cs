using Microsoft.EntityFrameworkCore;

public class CreateAttributeAssignmentHandler(IApplicationDbContext context)
  : ICommandHandler<CreateAttributeAssignmentCommand, Result<CreateAttributeAssignmentCommandResult>>
{
  public async Task<Result<CreateAttributeAssignmentCommandResult>> Handle(CreateAttributeAssignmentCommand command, CancellationToken cancellationToken)
  {
    var input = command.Assignment;
    var definitionId = AttributeDefinitionId.Of(input.AttributeDefinitionId);
    var definition = await context.AttributeDefinitions.AsNoTracking().FirstOrDefaultAsync(d => d.Id == definitionId, cancellationToken)
      ?? throw new AttributeDefinitionNotFoundException($"Attribute definition {input.AttributeDefinitionId} was not found.");

    if (!definition.IsActive)
      return Result<CreateAttributeAssignmentCommandResult>.Failure($"Attribute '{definition.Code.Value}' is inactive.");

    var target = AttributeScopeTarget.Of(input.Scope, input.AssetClassId, input.AssetTypeId, input.CategoryId, input.AssetId);
    await AttributeAssignmentTargets.EnsureExistsAsync(context, target, cancellationToken);

    var duplicate = await context.AttributeAssignments.AnyAsync(a =>
        a.AttributeDefinitionId == definitionId
        && a.AssetClassId == target.AssetClassId
        && a.AssetTypeId == target.AssetTypeId
        && a.CategoryId == target.CategoryId
        && a.AssetId == target.AssetId, cancellationToken);

    if (duplicate)
      return Result<CreateAttributeAssignmentCommandResult>.Failure($"Attribute '{definition.Code.Value}' is already assigned to this {input.Scope}.");

    AttributeGroupId? groupId = null;
    if (input.AttributeGroupId.HasValue)
    {
      groupId = AttributeGroupId.Of(input.AttributeGroupId.Value);
      if (!await context.AttributeGroups.AnyAsync(g => g.Id == groupId, cancellationToken))
        throw new AttributeGroupNotFoundException($"Attribute group {input.AttributeGroupId} was not found.");
    }

    if (input.DependsOnAssignmentId.HasValue)
    {
      var parentId = AttributeAssignmentId.Of(input.DependsOnAssignmentId.Value);
      if (!await context.AttributeAssignments.AnyAsync(a => a.Id == parentId, cancellationToken))
        throw new AttributeAssignmentNotFoundException($"Parent assignment {input.DependsOnAssignmentId} was not found.");
    }

    var assignment = AttributeAssignment.Create(
      AttributeAssignmentId.Of(Guid.NewGuid()),
      definitionId,
      target,
      groupId,
      input.ToOptions());

    if (!input.IsActive)
      assignment.Update(groupId, input.ToOptions(), false);

    await context.AttributeAssignments.AddAsync(assignment, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    return Result<CreateAttributeAssignmentCommandResult>.Success(new CreateAttributeAssignmentCommandResult(assignment.Id.Value));
  }
}

/// Shared existence checks for the four possible assignment targets.
public static class AttributeAssignmentTargets
{
  public static async Task EnsureExistsAsync(IApplicationDbContext context, AttributeScopeTarget target, CancellationToken cancellationToken)
  {
    switch (target.Scope)
    {
      case AttributeScope.AssetClass:
        if (!await context.AssetClasses.AnyAsync(c => c.Id == target.AssetClassId, cancellationToken))
          throw new AssetClassNotFoundException($"Asset class {target.AssetClassId!.Value} was not found.");
        break;

      case AttributeScope.AssetType:
        if (!await context.AssetTypes.AnyAsync(t => t.Id == target.AssetTypeId, cancellationToken))
          throw new AssetTypeNotFoundException($"Asset type {target.AssetTypeId!.Value} was not found.");
        break;

      case AttributeScope.Category:
        if (!await context.AssetCategories.AnyAsync(c => c.Id == target.CategoryId, cancellationToken))
          throw new AssetCategoryNotFoundException($"Asset category {target.CategoryId!.Value} was not found.");
        break;

      case AttributeScope.Asset:
        if (!await context.Assets.AnyAsync(a => a.Id == target.AssetId, cancellationToken))
          throw new AssetNotFoundException($"Asset {target.AssetId!.Value} was not found.");
        break;
    }
  }
}
