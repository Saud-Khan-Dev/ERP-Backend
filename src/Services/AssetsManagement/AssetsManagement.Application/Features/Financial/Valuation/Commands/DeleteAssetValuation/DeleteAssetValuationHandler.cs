using Microsoft.EntityFrameworkCore;

public class DeleteAssetValuationHandler(IApplicationDbContext context)
  : ICommandHandler<DeleteAssetValuationCommand, Result<DeleteAssetValuationCommandResult>>
{
  public async Task<Result<DeleteAssetValuationCommandResult>> Handle(DeleteAssetValuationCommand command, CancellationToken cancellationToken)
  {
    var assetId = AssetId.Of(command.AssetId);
    var valuationId = AssetValuationId.Of(command.ValuationId);

    var valuation = await context.AssetValuations.FirstOrDefaultAsync(v => v.Id == valuationId && v.AssetId == assetId, cancellationToken)
      ?? throw new AssetValuationNotFoundException($"Valuation {command.ValuationId} was not found on asset {command.AssetId}.");

    context.AssetValuations.Remove(valuation);
    await context.SaveChangesAsync(cancellationToken);

    return Result<DeleteAssetValuationCommandResult>.Success(new DeleteAssetValuationCommandResult(true));
  }
}
