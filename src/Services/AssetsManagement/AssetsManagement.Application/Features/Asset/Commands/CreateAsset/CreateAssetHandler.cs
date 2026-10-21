using System.Text.Json;
using Microsoft.EntityFrameworkCore;

public class CreateAssetHandler(
    IApplicationDbContext context, IAttributeSchemaService schemaService, IAssetDataQueries queries, IHrmDirectory hrm,
    ICurrentUser currentUser, IClock clock)
  : ICommandHandler<CreateAssetCommand, Result<CreateAssetCommandResult>>
{
  public async Task<Result<CreateAssetCommandResult>> Handle(CreateAssetCommand command, CancellationToken cancellationToken)
  {
    var input = command.Asset;
    var actor = currentUser.Actor();
    var today = clock.Today;

    // purchase and depreciation are money: registering them with the asset needs the finance right as well
    if (command.Acquisition is not null || command.Depreciation is not null)
      FinanceRights.Require(currentUser, PermissionCatalog.AssetFinance.Create,
        "Recording the purchase or the depreciation needs the Asset finance right to add records. Register the asset without them, and someone with that right adds them on its Finance tab.");

    AssetCode assetCode;
    if (string.IsNullOrWhiteSpace(input.AssetCode))
    {
      assetCode = await AssetCodeIssuer.NextAsync(queries, cancellationToken);
    }
    else
    {
      assetCode = AssetCode.Of(input.AssetCode);
      if (assetCode.Value.StartsWith(Asset.IssuedTagPrefix, StringComparison.Ordinal))
        return Result<CreateAssetCommandResult>.Failure($"Tags starting with {Asset.IssuedTagPrefix} are issued by the system. Leave the tag empty to get the next one, or use another prefix.");
      if (await context.Assets.IgnoreQueryFilters().AnyAsync(a => a.AssetCode == assetCode, cancellationToken))
        return Result<CreateAssetCommandResult>.Failure($"The tag {assetCode.Value} is already used by another asset.");
    }

    var taxonomy = await AssetTaxonomyLoader.LoadAsync(context, input.AssetClassId, input.AssetTypeId, input.CategoryId, cancellationToken);

    var statusId = AssetStatusId.Of(input.StatusId);
    var status = await context.AssetStatuses.AsNoTracking().FirstOrDefaultAsync(s => s.Id == statusId, cancellationToken)
      ?? throw new AssetStatusNotFoundException("The status was not found. Choose it again.");

    var identifierConflict = await AssetIdentifierChecks.FindConflictAsync(context, null, input.Barcode, cancellationToken);
    if (identifierConflict is not null)
      return Result<CreateAssetCommandResult>.Failure(identifierConflict);

    LocationId? locationId = null;
    if (input.CurrentLocationId.HasValue)
    {
      locationId = LocationId.Of(input.CurrentLocationId.Value);
      if (!await context.Locations.AnyAsync(l => l.Id == locationId && l.IsActive, cancellationToken))
        throw new LocationNotFoundException("The location was not found or is switched off. Choose it again.");
    }

    var custody = await CustodyResolver.ResolveAsync(hrm, input.CustodianId, input.DepartmentId, cancellationToken);

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
      custody: custody,
      currentLocationId: locationId,
      barcode: input.Barcode);

    await context.Assets.AddAsync(asset, cancellationToken);

    // validate + project + history; required attributes are enforced even when none were supplied
    await schemaService.ApplyAttributesAsync(
      asset,
      input.ExtraAttributes ?? new Dictionary<string, JsonElement>(),
      isNewAsset: true,
      actor,
      "Asset created",
      cancellationToken);

    if (command.Acquisition is { } purchase)
    {
      var currency = Currency.Of(purchase.CurrencyCode);
      if (!await context.Currencies.AnyAsync(c => c.Id == currency && c.IsActive, cancellationToken))
        throw new CurrencyNotFoundException($"The currency {currency.Value} was not found or is switched off.");

      var acquisition = AssetAcquisition.Create(
        AssetAcquisitionId.Of(Guid.NewGuid()), asset.Id, purchase.AcquisitionDate, purchase.AcquisitionCost, currency,
        purchase.ExchangeRate, purchase.SupplierName, purchase.PurchaseReference, purchase.AcquisitionType,
        purchase.WarrantyStartDate, purchase.WarrantyExpiryDate, today);
      await context.AssetAcquisitions.AddAsync(acquisition, cancellationToken);

      if (command.Depreciation is { } plan)
      {
        var methodId = DepreciationMethodId.Of(plan.MethodId);
        var method = await context.DepreciationMethods.AsNoTracking().FirstOrDefaultAsync(m => m.Id == methodId, cancellationToken)
          ?? throw new DepreciationMethodNotFoundException("The depreciation method was not found. Choose it again.");

        if (plan.StartDate is { } start && start < purchase.AcquisitionDate)
          return Result<CreateAssetCommandResult>.Failure("Depreciation cannot start before the asset was acquired.");

        var schedule = AssetDepreciationSchedule.Create(
          AssetDepreciationScheduleId.Of(Guid.NewGuid()), asset, taxonomy.Type, method, purchase.AcquisitionCost,
          plan.UsefulLifeMonths, plan.SalvageValue, plan.DecliningRate, plan.StartDate ?? purchase.AcquisitionDate);
        await context.AssetDepreciationSchedules.AddAsync(schedule, cancellationToken);
      }
    }

    await SystemEvents.AddAsync(context, asset.Id, EventPurpose.Registered, clock.UtcNow, actor, toStatusId: status.Id, cancellationToken: cancellationToken);

    await context.SaveChangesAsync(cancellationToken);

    return Result<CreateAssetCommandResult>.Success(new CreateAssetCommandResult(asset.Id.Value, asset.AssetCode.Value));
  }
}

/// Issues the next AST-000001 style tag from the database sequence (assets.asset_tag_seq): two registrations at the
/// same moment get different tags, and a number once taken is never handed out again.
public static class AssetCodeIssuer
{
  private const int Digits = 6;

  public static async Task<AssetCode> NextAsync(IAssetDataQueries queries, CancellationToken cancellationToken) =>
      Format(await queries.NextTagNumberAsync(cancellationToken));

  public static async Task<AssetCode> PeekAsync(IAssetDataQueries queries, CancellationToken cancellationToken) =>
      Format(await queries.PeekTagNumberAsync(cancellationToken));

  private static AssetCode Format(long number) => AssetCode.Of($"{Asset.IssuedTagPrefix}{number.ToString().PadLeft(Digits, '0')}");
}

/// Loads and cross-checks the class / type / category triple an asset is attached to.
public sealed record AssetTaxonomy(AssetClass Class, AssetType Type, AssetCategory Category);

public static class AssetTaxonomyLoader
{
  public static async Task<AssetTaxonomy> LoadAsync(IApplicationDbContext context, Guid classId, Guid typeId, Guid categoryId, CancellationToken cancellationToken)
  {
    var assetClassId = AssetClassId.Of(classId);
    var assetClass = await context.AssetClasses.AsNoTracking().FirstOrDefaultAsync(c => c.Id == assetClassId, cancellationToken)
      ?? throw new AssetClassNotFoundException("The asset class was not found. Choose it again.");

    var assetTypeId = AssetTypeId.Of(typeId);
    var assetType = await context.AssetTypes.AsNoTracking().FirstOrDefaultAsync(t => t.Id == assetTypeId, cancellationToken)
      ?? throw new AssetTypeNotFoundException("The asset type was not found. Choose it again.");

    var assetCategoryId = AssetCategoryId.Of(categoryId);
    var category = await context.AssetCategories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == assetCategoryId, cancellationToken)
      ?? throw new AssetCategoryNotFoundException("The category was not found. Choose it again.");

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
      return $"The barcode {barcode} is already used by another asset.";

    return null;
  }
}
