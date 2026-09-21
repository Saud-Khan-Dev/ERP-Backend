using Microsoft.EntityFrameworkCore;

public class DeleteAssetHandler(IApplicationDbContext context)
  : ICommandHandler<DeleteAssetCommand, Result<DeleteAssetCommandResult>>
{
  public async Task<Result<DeleteAssetCommandResult>> Handle(DeleteAssetCommand command, CancellationToken cancellationToken)
  {
    var id = AssetId.Of(command.Id);
    var asset = await context.Assets.FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
      ?? throw new AssetNotFoundException($"Asset {command.Id} was not found.");

    if (await context.Assets.AnyAsync(a => a.ParentAssetId == id, cancellationToken))
      return Result<DeleteAssetCommandResult>.Failure("This asset has component assets attached. Detach or delete them first.");

    if (await context.AssetDepreciationSchedules.AnyAsync(s => s.AssetId == id && s.IsActive, cancellationToken))
      return Result<DeleteAssetCommandResult>.Failure("This asset has an active depreciation schedule. Deactivate it first.");

    asset.SoftDelete(command.DeletedBy, DateTime.UtcNow);
    await context.SaveChangesAsync(cancellationToken);

    return Result<DeleteAssetCommandResult>.Success(new DeleteAssetCommandResult(true));
  }
}
