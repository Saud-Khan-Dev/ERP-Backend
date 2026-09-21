using Microsoft.EntityFrameworkCore;

public class DeleteAssetTypeHandler(IApplicationDbContext context)
  : ICommandHandler<DeleteAssetTypeCommand, Result<DeleteAssetTypeCommandResult>>
{
  public async Task<Result<DeleteAssetTypeCommandResult>> Handle(DeleteAssetTypeCommand command, CancellationToken cancellationToken)
  {
    var id = AssetTypeId.Of(command.Id);
    var assetType = await context.AssetTypes.FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
      ?? throw new AssetTypeNotFoundException($"Asset type {command.Id} was not found.");

    if (await context.AssetCategories.AnyAsync(c => c.AssetTypeId == id, cancellationToken)
        || await context.Assets.IgnoreQueryFilters().AnyAsync(a => a.AssetTypeId == id, cancellationToken))
      return Result<DeleteAssetTypeCommandResult>.Failure("This asset type is in use by categories or assets. Deactivate it instead.");

    context.AssetTypes.Remove(assetType);
    await context.SaveChangesAsync(cancellationToken);

    return Result<DeleteAssetTypeCommandResult>.Success(new DeleteAssetTypeCommandResult(true));
  }
}
