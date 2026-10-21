using Microsoft.EntityFrameworkCore;

public class DeleteAssetClassHandler(IApplicationDbContext context)
  : ICommandHandler<DeleteAssetClassCommand, Result<DeleteAssetClassCommandResult>>
{
  public async Task<Result<DeleteAssetClassCommandResult>> Handle(DeleteAssetClassCommand command, CancellationToken cancellationToken)
  {
    var id = AssetClassId.Of(command.Id);
    var assetClass = await context.AssetClasses.FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
      ?? throw new AssetClassNotFoundException($"Asset class {command.Id} was not found.");

    if (await context.AssetTypes.AnyAsync(t => t.AssetClassId == id, cancellationToken)
        || await context.AssetCategories.AnyAsync(c => c.AssetClassId == id, cancellationToken))
      return Result<DeleteAssetClassCommandResult>.Failure($"'{assetClass.Name.Value}' still has types or categories under it. Delete those first, or switch the class off.");

    if (await context.Assets.IgnoreQueryFilters().AnyAsync(a => a.AssetClassId == id, cancellationToken))
      return Result<DeleteAssetClassCommandResult>.Failure($"Assets (some may be deleted ones kept on record) belong to '{assetClass.Name.Value}', so it cannot be deleted. Switch it off instead.");

    context.AssetClasses.Remove(assetClass);
    await context.SaveChangesAsync(cancellationToken);

    return Result<DeleteAssetClassCommandResult>.Success(new DeleteAssetClassCommandResult(true));
  }
}
