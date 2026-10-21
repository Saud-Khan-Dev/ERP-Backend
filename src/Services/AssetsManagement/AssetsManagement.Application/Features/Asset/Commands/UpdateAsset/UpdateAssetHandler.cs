using System.Text.Json;
using Microsoft.EntityFrameworkCore;

public class UpdateAssetHandler(IApplicationDbContext context, IAttributeSchemaService schemaService, ICurrentUser currentUser, IClock clock)
  : ICommandHandler<UpdateAssetCommand, Result<UpdateAssetCommandResult>>
{
  public async Task<Result<UpdateAssetCommandResult>> Handle(UpdateAssetCommand command, CancellationToken cancellationToken)
  {
    var id = AssetId.Of(command.Id);
    var asset = await AssetRecordGuard.LoadAsync(context, id, track: true, cancellationToken);
    await AssetRecordGuard.EnsureOpenAsync(context, asset, cancellationToken);

    var input = command.Asset;
    var currentStatus = await context.AssetStatuses.AsNoTracking().FirstAsync(s => s.Id == asset.StatusId, cancellationToken);
    var taxonomy = await AssetTaxonomyLoader.LoadAsync(context, input.AssetClassId, input.AssetTypeId, input.CategoryId, cancellationToken);

    var identifierConflict = await AssetIdentifierChecks.FindConflictAsync(context, id, input.Barcode, cancellationToken);
    if (identifierConflict is not null)
      return Result<UpdateAssetCommandResult>.Failure(identifierConflict);

    var oldCategory = await context.AssetCategories.AsNoTracking().FirstAsync(c => c.Id == asset.CategoryId, cancellationToken);
    var changes = new List<(string, string?, string?)>
    {
      ("Name", asset.Name.Value, input.Name.Trim()),
      ("Description", asset.Description, string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim()),
      ("Ownership", asset.Ownership.ToString(), input.Ownership.ToString()),
      ("Barcode", asset.Barcode, string.IsNullOrWhiteSpace(input.Barcode) ? null : input.Barcode.Trim()),
      ("Category", oldCategory.Name.Value, taxonomy.Category.Name.Value),
    };

    asset.UpdateDetails(Name.Of(input.Name, 200), input.Description, input.Ownership, currentStatus, input.Barcode);

    var reclassified = asset.CategoryId != taxonomy.Category.Id || asset.AssetTypeId != taxonomy.Type.Id || asset.AssetClassId != taxonomy.Class.Id;
    if (reclassified)
      asset.Reclassify(taxonomy.Class, taxonomy.Type, taxonomy.Category, currentStatus);

    var actor = currentUser.Actor();

    // always re-run the engine: re-classification changes the schema, a patch changes the values
    await schemaService.ApplyAttributesAsync(
      asset,
      input.ExtraAttributes ?? new Dictionary<string, JsonElement>(),
      isNewAsset: false,
      actor,
      reclassified ? "Asset re-classified" : "Asset updated",
      cancellationToken);

    if (SystemEvents.Changes(changes) is { } details)
      await SystemEvents.AddAsync(context, id, EventPurpose.DetailsChanged, clock.UtcNow, actor, reclassified ? "Moved to another category" : "Details changed", details, cancellationToken: cancellationToken);

    await context.SaveChangesAsync(cancellationToken);

    return Result<UpdateAssetCommandResult>.Success(new UpdateAssetCommandResult(true));
  }
}
