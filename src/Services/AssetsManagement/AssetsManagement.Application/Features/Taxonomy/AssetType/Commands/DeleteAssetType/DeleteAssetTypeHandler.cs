using Microsoft.EntityFrameworkCore;

public class DeleteAssetTypeHandler(IApplicationDbContext context)
  : ICommandHandler<DeleteAssetTypeCommand, Result<DeleteAssetTypeCommandResult>>
{
  public async Task<Result<DeleteAssetTypeCommandResult>> Handle(DeleteAssetTypeCommand command, CancellationToken cancellationToken)
  {
    var id = AssetTypeId.Of(command.Id);
    var assetType = await context.AssetTypes.FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
      ?? throw new AssetTypeNotFoundException($"Asset type {command.Id} was not found.");

    if (await context.AssetCategories.AnyAsync(c => c.AssetTypeId == id, cancellationToken))
      return Result<DeleteAssetTypeCommandResult>.Failure($"'{assetType.Name.Value}' still has categories under it. Delete those first, or switch the type off.");

    if (await context.Assets.IgnoreQueryFilters().AnyAsync(a => a.AssetTypeId == id, cancellationToken))
      return Result<DeleteAssetTypeCommandResult>.Failure($"Assets (some may be deleted ones kept on record) are of type '{assetType.Name.Value}', so it cannot be deleted. Switch it off instead.");

    context.AssetTypes.Remove(assetType);
    await context.SaveChangesAsync(cancellationToken);

    return Result<DeleteAssetTypeCommandResult>.Success(new DeleteAssetTypeCommandResult(true));
  }
}
