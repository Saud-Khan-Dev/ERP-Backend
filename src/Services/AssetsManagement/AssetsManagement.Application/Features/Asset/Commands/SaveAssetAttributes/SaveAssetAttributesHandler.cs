using Microsoft.EntityFrameworkCore;

public class SaveAssetAttributesHandler(IApplicationDbContext context, IAttributeSchemaService schemaService)
  : ICommandHandler<SaveAssetAttributesCommand, Result<SaveAssetAttributesCommandResult>>
{
  public async Task<Result<SaveAssetAttributesCommandResult>> Handle(SaveAssetAttributesCommand command, CancellationToken cancellationToken)
  {
    var assetId = AssetId.Of(command.AssetId);
    var asset = await context.Assets.FirstOrDefaultAsync(a => a.Id == assetId, cancellationToken)
      ?? throw new AssetNotFoundException($"Asset {command.AssetId} was not found.");

    var currentStatus = await context.AssetStatuses.AsNoTracking().FirstAsync(s => s.Id == asset.StatusId, cancellationToken);
    asset.EnsureEditable(currentStatus);

    await schemaService.ApplyAttributesAsync(asset, command.Attributes, isNewAsset: false, command.ChangedBy, command.ChangeReason, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    return Result<SaveAssetAttributesCommandResult>.Success(
      new SaveAssetAttributesCommandResult(asset.ExtraAttributes, asset.AttributesValidatedAt));
  }
}
