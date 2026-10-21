using Microsoft.EntityFrameworkCore;

public class DeleteAssetCategoryHandler(IApplicationDbContext context)
  : ICommandHandler<DeleteAssetCategoryCommand, Result<DeleteAssetCategoryCommandResult>>
{
  public async Task<Result<DeleteAssetCategoryCommandResult>> Handle(DeleteAssetCategoryCommand command, CancellationToken cancellationToken)
  {
    var id = AssetCategoryId.Of(command.Id);
    var category = await context.AssetCategories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
      ?? throw new AssetCategoryNotFoundException($"Asset category {command.Id} was not found.");

    if (await context.AssetCategories.AnyAsync(c => c.ParentCategoryId == id, cancellationToken))
      return Result<DeleteAssetCategoryCommandResult>.Failure($"'{category.Name.Value}' has sub-categories. Delete or move them first.");

    var live = await context.Assets.CountAsync(a => a.CategoryId == id, cancellationToken);
    if (live > 0)
      return Result<DeleteAssetCategoryCommandResult>.Failure($"{live} {(live == 1 ? "asset is" : "assets are")} in '{category.Name.Value}'. Move them to another category first, or switch the category off.");

    if (await context.Assets.IgnoreQueryFilters().AnyAsync(a => a.CategoryId == id, cancellationToken))
      return Result<DeleteAssetCategoryCommandResult>.Failure($"Deleted assets are kept on record in '{category.Name.Value}', so it cannot be deleted. Switch it off instead - it then disappears from the forms.");

    context.AssetCategories.Remove(category);

    if (category.ParentCategoryId is not null)
    {
      var parent = await context.AssetCategories.FirstOrDefaultAsync(c => c.Id == category.ParentCategoryId, cancellationToken);
      var siblings = await context.AssetCategories.CountAsync(c => c.ParentCategoryId == category.ParentCategoryId && c.Id != id, cancellationToken);
      if (parent is not null && siblings == 0)
        parent.MarkAsLeaf();
    }

    await context.SaveChangesAsync(cancellationToken);

    return Result<DeleteAssetCategoryCommandResult>.Success(new DeleteAssetCategoryCommandResult(true));
  }
}
