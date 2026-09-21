using System.Text.Json;
using Microsoft.EntityFrameworkCore;

public class CreateAssetHandler(IApplicationDbContext context, IAttributeSchemaService schemaService)
  : ICommandHandler<CreateAssetCommand, Result<CreateAssetCommandResult>>
{
  public async Task<Result<CreateAssetCommandResult>> Handle(CreateAssetCommand command, CancellationToken cancellationToken)
  {
    var input = command.Asset;
    var assetCode = AssetCode.Of(input.AssetCode);

    if (await context.Assets.IgnoreQueryFilters().AnyAsync(a => a.AssetCode == assetCode, cancellationToken))
      return Result<CreateAssetCommandResult>.Failure($"An asset with code {assetCode.Value} already exists.");

    var taxonomy = await AssetTaxonomyLoader.LoadAsync(context, input.AssetClassId, input.AssetTypeId, input.CategoryId, cancellationToken);

    var statusId = AssetStatusId.Of(input.StatusId);
    var status = await context.AssetStatuses.AsNoTracking().FirstOrDefaultAsync(s => s.Id == statusId, cancellationToken)
      ?? throw new AssetStatusNotFoundException($"Asset status {input.StatusId} was not found.");

    var identifierConflict = await AssetIdentifierChecks.FindConflictAsync(context, null, taxonomy.Category.Id, input.SerialNumber, input.Barcode, input.RfidTag, cancellationToken);
    if (identifierConflict is not null)
      return Result<CreateAssetCommandResult>.Failure(identifierConflict);

    LocationId? locationId = null;
    if (input.CurrentLocationId.HasValue)
    {
      locationId = LocationId.Of(input.CurrentLocationId.Value);
      if (!await context.Locations.AnyAsync(l => l.Id == locationId, cancellationToken))
        throw new LocationNotFoundException($"Location {input.CurrentLocationId} was not found.");
    }

    AssetId? parentId = null;
    if (input.ParentAssetId.HasValue)
    {
      parentId = AssetId.Of(input.ParentAssetId.Value);
      if (!await context.Assets.AnyAsync(a => a.Id == parentId, cancellationToken))
        throw new AssetNotFoundException($"Parent asset {input.ParentAssetId} was not found.");
    }

    var asset = Asset.Create(
      id: AssetId.Of(Guid.NewGuid()),
      assetCode: assetCode,
      name: Name.Of(input.Name, 200),
      description: input.Description,
      ownership: input.Ownership,
      assetClass: taxonomy.Class,
      assetType: taxonomy.Type,
      category: taxonomy.Category,
      status: status,
      parentAssetId: parentId,
      departmentId: input.DepartmentId,
      custodianId: input.CustodianId,
      currentLocationId: locationId,
      serialNumber: input.SerialNumber,
      barcode: input.Barcode,
      rfidTag: input.RfidTag);

    await context.Assets.AddAsync(asset, cancellationToken);

    // validate + project + history; required attributes are enforced even when none were supplied
    await schemaService.ApplyAttributesAsync(
      asset,
      input.ExtraAttributes ?? new Dictionary<string, JsonElement>(),
      isNewAsset: true,
      command.PerformedBy,
      "Asset created",
      cancellationToken);

    await context.SaveChangesAsync(cancellationToken);

    return Result<CreateAssetCommandResult>.Success(new CreateAssetCommandResult(asset.Id.Value));
  }
}

/// Loads and cross-checks the class / type / category triple an asset is attached to.
public sealed record AssetTaxonomy(AssetClass Class, AssetType Type, AssetCategory Category);

public static class AssetTaxonomyLoader
{
  public static async Task<AssetTaxonomy> LoadAsync(IApplicationDbContext context, Guid classId, Guid typeId, Guid categoryId, CancellationToken cancellationToken)
  {
    var assetClassId = AssetClassId.Of(classId);
    var assetClass = await context.AssetClasses.AsNoTracking().FirstOrDefaultAsync(c => c.Id == assetClassId, cancellationToken)
      ?? throw new AssetClassNotFoundException($"Asset class {classId} was not found.");

    var assetTypeId = AssetTypeId.Of(typeId);
    var assetType = await context.AssetTypes.AsNoTracking().FirstOrDefaultAsync(t => t.Id == assetTypeId, cancellationToken)
      ?? throw new AssetTypeNotFoundException($"Asset type {typeId} was not found.");

    var assetCategoryId = AssetCategoryId.Of(categoryId);
    var category = await context.AssetCategories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == assetCategoryId, cancellationToken)
      ?? throw new AssetCategoryNotFoundException($"Asset category {categoryId} was not found.");

    return new AssetTaxonomy(assetClass, assetType, category);
  }
}

public static class AssetIdentifierChecks
{
  /// Barcode / RFID are globally unique, serial numbers are unique within a category (matches the DB indexes).
  public static async Task<string?> FindConflictAsync(
      IApplicationDbContext context,
      AssetId? self,
      AssetCategoryId categoryId,
      string? serialNumber,
      string? barcode,
      string? rfidTag,
      CancellationToken cancellationToken)
  {
    var assets = context.Assets.IgnoreQueryFilters();
    if (self is not null)
      assets = assets.Where(a => a.Id != self);

    serialNumber = string.IsNullOrWhiteSpace(serialNumber) ? null : serialNumber.Trim();
    barcode = string.IsNullOrWhiteSpace(barcode) ? null : barcode.Trim();
    rfidTag = string.IsNullOrWhiteSpace(rfidTag) ? null : rfidTag.Trim();

    if (serialNumber is not null && await assets.AnyAsync(a => a.CategoryId == categoryId && a.SerialNumber == serialNumber, cancellationToken))
      return $"Serial number {serialNumber} is already used by another asset in this category.";

    if (barcode is not null && await assets.AnyAsync(a => a.Barcode == barcode, cancellationToken))
      return $"Barcode {barcode} is already used by another asset.";

    if (rfidTag is not null && await assets.AnyAsync(a => a.RfidTag == rfidTag, cancellationToken))
      return $"RFID tag {rfidTag} is already used by another asset.";

    return null;
  }
}
