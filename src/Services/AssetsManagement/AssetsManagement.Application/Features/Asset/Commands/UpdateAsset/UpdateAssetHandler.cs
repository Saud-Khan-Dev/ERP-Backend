using System.Text.Json;
using Microsoft.EntityFrameworkCore;

public class UpdateAssetHandler(IApplicationDbContext context, IAttributeSchemaService schemaService)
  : ICommandHandler<UpdateAssetCommand, Result<UpdateAssetCommandResult>>
{
  public async Task<Result<UpdateAssetCommandResult>> Handle(UpdateAssetCommand command, CancellationToken cancellationToken)
  {
    var id = AssetId.Of(command.Id);
    var asset = await context.Assets.FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
      ?? throw new AssetNotFoundException($"Asset {command.Id} was not found.");

    var input = command.Asset;
    var assetCode = AssetCode.Of(input.AssetCode);

    if (await context.Assets.IgnoreQueryFilters().AnyAsync(a => a.Id != id && a.AssetCode == assetCode, cancellationToken))
      return Result<UpdateAssetCommandResult>.Failure($"An asset with code {assetCode.Value} already exists.");

    var currentStatus = await context.AssetStatuses.AsNoTracking().FirstAsync(s => s.Id == asset.StatusId, cancellationToken);
    var taxonomy = await AssetTaxonomyLoader.LoadAsync(context, input.AssetClassId, input.AssetTypeId, input.CategoryId, cancellationToken);

    var identifierConflict = await AssetIdentifierChecks.FindConflictAsync(context, id, input.Barcode, cancellationToken);
    if (identifierConflict is not null)
      return Result<UpdateAssetCommandResult>.Failure(identifierConflict);

    asset.UpdateDetails(
      assetCode,
      Name.Of(input.Name, 200),
      input.Description,
      input.Ownership,
      currentStatus,
      input.Barcode,
      input.IsActive);

    var reclassified = asset.CategoryId != taxonomy.Category.Id || asset.AssetTypeId != taxonomy.Type.Id || asset.AssetClassId != taxonomy.Class.Id;
    if (reclassified)
      asset.Reclassify(taxonomy.Class, taxonomy.Type, taxonomy.Category, currentStatus);

    // always re-run the engine: re-classification changes the schema, a patch changes the values
    await schemaService.ApplyAttributesAsync(
      asset,
      input.ExtraAttributes ?? new Dictionary<string, JsonElement>(),
      isNewAsset: false,
      command.PerformedBy,
      reclassified ? "Asset re-classified" : "Asset updated",
      cancellationToken);

    await context.SaveChangesAsync(cancellationToken);

    return Result<UpdateAssetCommandResult>.Success(new UpdateAssetCommandResult(true));
  }
}
