using System.Globalization;
using Microsoft.EntityFrameworkCore;

public class UpsertAssetAcquisitionHandler(IApplicationDbContext context, ICurrentUser currentUser, IClock clock)
  : ICommandHandler<UpsertAssetAcquisitionCommand, Result<UpsertAssetAcquisitionCommandResult>>
{
  public async Task<Result<UpsertAssetAcquisitionCommandResult>> Handle(UpsertAssetAcquisitionCommand command, CancellationToken cancellationToken)
  {
    var assetId = AssetId.Of(command.AssetId);
    var asset = await AssetRecordGuard.LoadAsync(context, assetId, track: false, cancellationToken);
    await AssetRecordGuard.EnsureOpenAsync(context, asset, cancellationToken);

    var input = command.Acquisition;
    var currency = Currency.Of(input.CurrencyCode);
    if (!await context.Currencies.AnyAsync(c => c.Id == currency && c.IsActive, cancellationToken))
      throw new CurrencyNotFoundException($"The currency {currency.Value} was not found or is switched off.");

    var existing = await context.AssetAcquisitions.FirstOrDefaultAsync(a => a.AssetId == assetId, cancellationToken);
    var actor = currentUser.Actor();

    if (existing is null)
    {
      FinanceRights.Require(currentUser, PermissionCatalog.AssetFinance.Create, "Recording a purchase needs the Asset finance right to add records.");

      var acquisition = AssetAcquisition.Create(
        AssetAcquisitionId.Of(Guid.NewGuid()), assetId, input.AcquisitionDate, input.AcquisitionCost, currency,
        input.ExchangeRate, input.SupplierName, input.PurchaseReference, input.AcquisitionType,
        input.WarrantyStartDate, input.WarrantyExpiryDate, clock.Today);

      await context.AssetAcquisitions.AddAsync(acquisition, cancellationToken);
      await SystemEvents.AddAsync(context, assetId, EventPurpose.DetailsChanged, clock.UtcNow, actor, "Purchase recorded",
        SystemEvents.Changes([("Cost", null, Money(input.AcquisitionCost, currency.Value)), ("Purchased on", null, Day(input.AcquisitionDate))]),
        cancellationToken: cancellationToken);
      await context.SaveChangesAsync(cancellationToken);

      return Result<UpsertAssetAcquisitionCommandResult>.Success(new UpsertAssetAcquisitionCommandResult(acquisition.Id.Value, true));
    }

    FinanceRights.Require(currentUser, PermissionCatalog.AssetFinance.Edit, "Changing a purchase needs the Asset finance right to edit records.");

    var schedules = await context.AssetDepreciationSchedules.AsNoTracking().Include(s => s.Entries).Where(s => s.AssetId == assetId).ToListAsync(cancellationToken);
    var changes = new List<(string, string?, string?)>
    {
      ("Cost", Money(existing.AcquisitionCost, existing.CurrencyCode.Value), Money(input.AcquisitionCost, currency.Value)),
      ("Purchased on", Day(existing.AcquisitionDate), Day(input.AcquisitionDate)),
      ("How", existing.AcquisitionType.ToString(), input.AcquisitionType.ToString()),
      ("Supplier", existing.SupplierName, Clean(input.SupplierName)),
      ("Purchase reference", existing.PurchaseReference, Clean(input.PurchaseReference)),
      ("Warranty until", Day(existing.WarrantyExpiryDate), Day(input.WarrantyExpiryDate)),
    };

    existing.Update(
      input.AcquisitionDate, input.AcquisitionCost, currency, input.ExchangeRate, input.SupplierName,
      input.PurchaseReference, input.AcquisitionType, input.WarrantyStartDate, input.WarrantyExpiryDate,
      clock.Today,
      depreciationPosted: schedules.Any(s => s.HasPostedEntries),
      scheduleActive: schedules.Any(s => s.IsActive));

    if (SystemEvents.Changes(changes) is { } details)
      await SystemEvents.AddAsync(context, assetId, EventPurpose.DetailsChanged, clock.UtcNow, actor, "Purchase details changed", details, cancellationToken: cancellationToken);

    await context.SaveChangesAsync(cancellationToken);

    return Result<UpsertAssetAcquisitionCommandResult>.Success(new UpsertAssetAcquisitionCommandResult(existing.Id.Value, false));
  }

  private static string Money(decimal amount, string currency) => $"{amount.ToString("N2", CultureInfo.InvariantCulture)} {currency}";

  private static string? Day(DateOnly? date) => date?.ToString("d MMM yyyy", CultureInfo.InvariantCulture);

  private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
