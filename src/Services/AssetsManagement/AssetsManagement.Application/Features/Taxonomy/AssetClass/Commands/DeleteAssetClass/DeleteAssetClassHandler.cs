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
        || await context.AssetCategories.AnyAsync(c => c.AssetClassId == id, cancellationToken)
        || await context.Assets.IgnoreQueryFilters().AnyAsync(a => a.AssetClassId == id, cancellationToken))
      return Result<DeleteAssetClassCommandResult>.Failure("This asset class is in use by types, categories or assets. Deactivate it instead.");

    context.AssetClasses.Remove(assetClass);
    await context.SaveChangesAsync(cancellationToken);

    return Result<DeleteAssetClassCommandResult>.Success(new DeleteAssetClassCommandResult(true));
  }
}
