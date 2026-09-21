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
      return Result<DeleteAssetCategoryCommandResult>.Failure("This category has sub-categories. Delete or move them first.");

    if (await context.Assets.IgnoreQueryFilters().AnyAsync(a => a.CategoryId == id, cancellationToken))
      return Result<DeleteAssetCategoryCommandResult>.Failure("This category has assets attached. Move them first or deactivate the category.");

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
