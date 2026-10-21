using Microsoft.EntityFrameworkCore;

public class SaveAssetAttributesHandler(IApplicationDbContext context, IAttributeSchemaService schemaService, ICurrentUser currentUser)
  : ICommandHandler<SaveAssetAttributesCommand, Result<SaveAssetAttributesCommandResult>>
{
  public async Task<Result<SaveAssetAttributesCommandResult>> Handle(SaveAssetAttributesCommand command, CancellationToken cancellationToken)
  {
    var assetId = AssetId.Of(command.AssetId);
    var asset = await AssetRecordGuard.LoadAsync(context, assetId, track: true, cancellationToken);
    await AssetRecordGuard.EnsureOpenAsync(context, asset, cancellationToken);

    await schemaService.ApplyAttributesAsync(asset, command.Attributes, isNewAsset: false, currentUser.Actor(), command.ChangeReason, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    return Result<SaveAssetAttributesCommandResult>.Success(
      new SaveAssetAttributesCommandResult(asset.ExtraAttributes, asset.AttributesValidatedAt));
  }
}
