using Microsoft.EntityFrameworkCore;

public class DeleteAssetValuationHandler(IApplicationDbContext context)
  : ICommandHandler<DeleteAssetValuationCommand, Result<DeleteAssetValuationCommandResult>>
{
  public async Task<Result<DeleteAssetValuationCommandResult>> Handle(DeleteAssetValuationCommand command, CancellationToken cancellationToken)
  {
    var assetId = AssetId.Of(command.AssetId);
    var valuationId = AssetValuationId.Of(command.ValuationId);

    var asset = await AssetRecordGuard.LoadAsync(context, assetId, track: false, cancellationToken);
    await AssetRecordGuard.EnsureOpenAsync(context, asset, cancellationToken);

    var valuation = await context.AssetValuations.FirstOrDefaultAsync(v => v.Id == valuationId && v.AssetId == assetId, cancellationToken)
      ?? throw new AssetValuationNotFoundException("That valuation was not found on this asset.");

    context.AssetValuations.Remove(valuation);
    await context.SaveChangesAsync(cancellationToken);

    return Result<DeleteAssetValuationCommandResult>.Success(new DeleteAssetValuationCommandResult(true));
  }
}
