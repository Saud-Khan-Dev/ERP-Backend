using Microsoft.EntityFrameworkCore;

public class UpdateAssetCategoryHandler(IApplicationDbContext context)
  : ICommandHandler<UpdateAssetCategoryCommand, Result<UpdateAssetCategoryCommandResult>>
{
  public async Task<Result<UpdateAssetCategoryCommandResult>> Handle(UpdateAssetCategoryCommand command, CancellationToken cancellationToken)
  {
    var id = AssetCategoryId.Of(command.Id);
    var input = command.Category;

    // the whole class tree is loaded once: paths of every descendant may need rebuilding
    var classId = AssetClassId.Of(input.AssetClassId);
    var tree = await context.AssetCategories.Where(c => c.AssetClassId == classId).ToListAsync(cancellationToken);
    var category = tree.FirstOrDefault(c => c.Id == id)
      ?? throw new AssetCategoryNotFoundException($"Asset category {command.Id} was not found in class {input.AssetClassId}.");

    if (AssetTypeId.Of(input.AssetTypeId) != category.AssetTypeId)
      return Result<UpdateAssetCategoryCommandResult>.Failure("A category stays under the asset type it was created in. Create it under the other type instead.");

    var code = LookupCode.Of(input.Code);
    if (tree.Any(c => c.Id != id && c.Code == code))
      return Result<UpdateAssetCategoryCommandResult>.Failure($"A category with code {code.Value} already exists in this class.");

    AssetCategory? newParent = null;
    if (input.ParentCategoryId.HasValue)
    {
      var parentId = AssetCategoryId.Of(input.ParentCategoryId.Value);
      newParent = tree.FirstOrDefault(c => c.Id == parentId)
        ?? throw new AssetCategoryNotFoundException($"Parent category {input.ParentCategoryId} was not found in this class.");
    }

    var oldParentId = category.ParentCategoryId;
    var parentChanged = oldParentId != newParent?.Id;

    if (parentChanged && newParent is not null && newParent.IsLeaf
        && await context.Assets.IgnoreQueryFilters().AnyAsync(a => a.CategoryId == newParent.Id, cancellationToken))
      return Result<UpdateAssetCategoryCommandResult>.Failure($"'{newParent.Name.Value}' already has assets in it, so it cannot get sub-categories. Move its assets first.");

    category.Update(code, Name.Of(input.Name, 150), input.Description, input.DisplayOrder, input.IsActive);

    // Rebuild path/depth for the node and its whole subtree (code or parent may have changed).
    var byId = tree.ToDictionary(c => c.Id);
    var currentParent = newParent ?? (oldParentId is null ? null : byId[oldParentId]);
    category.Rebase(currentParent);
    RebaseDescendants(category, tree, byId);

    if (parentChanged)
    {
      newParent?.MarkAsBranch();

      if (oldParentId is not null && byId.TryGetValue(oldParentId, out var oldParent)
          && !tree.Any(c => c.Id != id && c.ParentCategoryId == oldParentId))
        oldParent.MarkAsLeaf();
    }

    await context.SaveChangesAsync(cancellationToken);

    return Result<UpdateAssetCategoryCommandResult>.Success(new UpdateAssetCategoryCommandResult(true));
  }

  private static void RebaseDescendants(AssetCategory root, List<AssetCategory> tree, Dictionary<AssetCategoryId, AssetCategory> byId)
  {
    var queue = new Queue<AssetCategory>();
    queue.Enqueue(root);

    while (queue.Count > 0)
    {
      var parent = queue.Dequeue();
      foreach (var child in tree.Where(c => c.ParentCategoryId == parent.Id))
      {
        child.Rebase(parent);
        queue.Enqueue(child);
      }
    }
  }
}
