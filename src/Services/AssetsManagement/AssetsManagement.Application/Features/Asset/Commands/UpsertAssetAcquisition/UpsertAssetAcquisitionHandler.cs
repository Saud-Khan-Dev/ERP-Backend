using Microsoft.EntityFrameworkCore;

public class UpsertAssetAcquisitionHandler(IApplicationDbContext context)
  : ICommandHandler<UpsertAssetAcquisitionCommand, Result<UpsertAssetAcquisitionCommandResult>>
{
  public async Task<Result<UpsertAssetAcquisitionCommandResult>> Handle(UpsertAssetAcquisitionCommand command, CancellationToken cancellationToken)
  {
    var assetId = AssetId.Of(command.AssetId);
    if (!await context.Assets.AnyAsync(a => a.Id == assetId, cancellationToken))
      throw new AssetNotFoundException($"Asset {command.AssetId} was not found.");

    var input = command.Acquisition;
    var currency = Currency.Of(input.CurrencyCode);
    if (!await context.Currencies.AnyAsync(c => c.Id == currency && c.IsActive, cancellationToken))
      throw new CurrencyNotFoundException($"Currency {currency.Value} was not found or is inactive.");

    var existing = await context.AssetAcquisitions.FirstOrDefaultAsync(a => a.AssetId == assetId, cancellationToken);

    if (existing is null)
    {
      var acquisition = AssetAcquisition.Create(
        AssetAcquisitionId.Of(Guid.NewGuid()), assetId, input.AcquisitionDate, input.AcquisitionCost, currency,
        input.ExchangeRate, input.SupplierId, input.PurchaseReference, input.AcquisitionType,
        input.WarrantyStartDate, input.WarrantyExpiryDate);

      await context.AssetAcquisitions.AddAsync(acquisition, cancellationToken);
      await context.SaveChangesAsync(cancellationToken);

      return Result<UpsertAssetAcquisitionCommandResult>.Success(new UpsertAssetAcquisitionCommandResult(acquisition.Id.Value, true));
    }

    if (await context.AssetDepreciationSchedules.AnyAsync(s => s.AssetId == assetId && s.IsActive, cancellationToken)
        && existing.AcquisitionCost != input.AcquisitionCost)
      return Result<UpsertAssetAcquisitionCommandResult>.Failure("The acquisition cost cannot change while an active depreciation schedule exists.");

    existing.Update(
      input.AcquisitionDate, input.AcquisitionCost, currency, input.ExchangeRate, input.SupplierId,
      input.PurchaseReference, input.AcquisitionType, input.WarrantyStartDate, input.WarrantyExpiryDate);

    await context.SaveChangesAsync(cancellationToken);

    return Result<UpsertAssetAcquisitionCommandResult>.Success(new UpsertAssetAcquisitionCommandResult(existing.Id.Value, false));
  }
}
