using System.Text.Json;
using Microsoft.EntityFrameworkCore;

public class CreateAssetHandler(IApplicationDbContext context, IAttributeSchemaService schemaService)
  : ICommandHandler<CreateAssetCommand, Result<CreateAssetCommandResult>>
{
  public async Task<Result<CreateAssetCommandResult>> Handle(CreateAssetCommand command, CancellationToken cancellationToken)
  {
    var input = command.Asset;

    AssetCode assetCode;
    if (string.IsNullOrWhiteSpace(input.AssetCode))
    {
      assetCode = await AssetCodeIssuer.NextAsync(context, cancellationToken);
    }
    else
    {
      assetCode = AssetCode.Of(input.AssetCode);
      if (await context.Assets.IgnoreQueryFilters().AnyAsync(a => a.AssetCode == assetCode, cancellationToken))
        return Result<CreateAssetCommandResult>.Failure($"An asset with code {assetCode.Value} already exists.");
    }

    var taxonomy = await AssetTaxonomyLoader.LoadAsync(context, input.AssetClassId, input.AssetTypeId, input.CategoryId, cancellationToken);

    var statusId = AssetStatusId.Of(input.StatusId);
    var status = await context.AssetStatuses.AsNoTracking().FirstOrDefaultAsync(s => s.Id == statusId, cancellationToken)
      ?? throw new AssetStatusNotFoundException($"Asset status {input.StatusId} was not found.");

    var identifierConflict = await AssetIdentifierChecks.FindConflictAsync(context, null, input.Barcode, cancellationToken);
    if (identifierConflict is not null)
      return Result<CreateAssetCommandResult>.Failure(identifierConflict);

    LocationId? locationId = null;
    if (input.CurrentLocationId.HasValue)
    {
      locationId = LocationId.Of(input.CurrentLocationId.Value);
      if (!await context.Locations.AnyAsync(l => l.Id == locationId && l.IsActive, cancellationToken))
        throw new LocationNotFoundException($"Location {input.CurrentLocationId} was not found or is inactive.");
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
      departmentId: input.DepartmentId,
      custodianId: input.CustodianId,
      currentLocationId: locationId,
      barcode: input.Barcode);

    await context.Assets.AddAsync(asset, cancellationToken);

    // validate + project + history; required attributes are enforced even when none were supplied
    await schemaService.ApplyAttributesAsync(
      asset,
      input.ExtraAttributes ?? new Dictionary<string, JsonElement>(),
      isNewAsset: true,
      command.PerformedBy,
      "Asset created",
      cancellationToken);

    if (command.Acquisition is { } purchase)
    {
      var currency = Currency.Of(purchase.CurrencyCode);
      if (!await context.Currencies.AnyAsync(c => c.Id == currency && c.IsActive, cancellationToken))
        throw new CurrencyNotFoundException($"Currency {currency.Value} was not found or is inactive.");

      var acquisition = AssetAcquisition.Create(
        AssetAcquisitionId.Of(Guid.NewGuid()), asset.Id, purchase.AcquisitionDate, purchase.AcquisitionCost, currency,
        purchase.ExchangeRate, purchase.SupplierId, purchase.PurchaseReference, purchase.AcquisitionType,
        purchase.WarrantyStartDate, purchase.WarrantyExpiryDate);
      await context.AssetAcquisitions.AddAsync(acquisition, cancellationToken);

      if (command.Depreciation is { } plan)
      {
        var methodId = DepreciationMethodId.Of(plan.MethodId);
        var method = await context.DepreciationMethods.AsNoTracking().FirstOrDefaultAsync(m => m.Id == methodId, cancellationToken)
          ?? throw new DepreciationMethodNotFoundException($"Depreciation method {plan.MethodId} was not found.");

        var schedule = AssetDepreciationSchedule.Create(
          AssetDepreciationScheduleId.Of(Guid.NewGuid()), asset, taxonomy.Type, method, purchase.AcquisitionCost,
          plan.UsefulLifeMonths, plan.SalvageValue, plan.DecliningRate, plan.StartDate ?? purchase.AcquisitionDate);
        await context.AssetDepreciationSchedules.AddAsync(schedule, cancellationToken);
      }
    }

    await context.SaveChangesAsync(cancellationToken);

    return Result<CreateAssetCommandResult>.Success(new CreateAssetCommandResult(asset.Id.Value, asset.AssetCode.Value));
  }
}

/// Issues the next AST-000001 style code. Deleted assets keep their codes, so they are counted too.
public static class AssetCodeIssuer
{
  public const string Prefix = "AST-";
  private const int Digits = 6;

  public static async Task<AssetCode> NextAsync(IApplicationDbContext context, CancellationToken cancellationToken)
  {
    var codes = await context.Assets.IgnoreQueryFilters()
      .Select(a => a.AssetCode)
      .ToListAsync(cancellationToken);

    var highest = 0;
    foreach (var code in codes)
    {
      var value = code.Value;
      if (value.StartsWith(Prefix, StringComparison.Ordinal) && int.TryParse(value[Prefix.Length..], out var number))
        highest = Math.Max(highest, number);
    }

    return AssetCode.Of($"{Prefix}{(highest + 1).ToString().PadLeft(Digits, '0')}");
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
  /// Barcodes are globally unique (matches the DB index); deleted assets keep theirs.
  public static async Task<string?> FindConflictAsync(
      IApplicationDbContext context,
      AssetId? self,
      string? barcode,
      CancellationToken cancellationToken)
  {
    var assets = context.Assets.IgnoreQueryFilters();
    if (self is not null)
      assets = assets.Where(a => a.Id != self);

    barcode = string.IsNullOrWhiteSpace(barcode) ? null : barcode.Trim();

    if (barcode is not null && await assets.AnyAsync(a => a.Barcode == barcode, cancellationToken))
      return $"Barcode {barcode} is already used by another asset.";

    return null;
  }
}
