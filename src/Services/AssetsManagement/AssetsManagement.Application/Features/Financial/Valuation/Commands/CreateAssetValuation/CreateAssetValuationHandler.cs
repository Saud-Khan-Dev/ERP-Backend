using Microsoft.EntityFrameworkCore;

public class CreateAssetValuationHandler(IApplicationDbContext context)
  : ICommandHandler<CreateAssetValuationCommand, Result<CreateAssetValuationCommandResult>>
{
  public async Task<Result<CreateAssetValuationCommandResult>> Handle(CreateAssetValuationCommand command, CancellationToken cancellationToken)
  {
    var assetId = AssetId.Of(command.AssetId);
    if (!await context.Assets.AnyAsync(a => a.Id == assetId, cancellationToken))
      throw new AssetNotFoundException($"Asset {command.AssetId} was not found.");

    var currency = Currency.Of(command.CurrencyCode);
    if (!await context.Currencies.AnyAsync(c => c.Id == currency && c.IsActive, cancellationToken))
      throw new CurrencyNotFoundException($"Currency {currency.Value} was not found or is inactive.");

    if (await context.AssetValuations.AnyAsync(v => v.AssetId == assetId && v.ValuationDate == command.ValuationDate, cancellationToken))
      return Result<CreateAssetValuationCommandResult>.Failure($"A valuation for {command.ValuationDate:yyyy-MM-dd} already exists for this asset.");

    var valuation = AssetValuation.Create(
      AssetValuationId.Of(Guid.NewGuid()), assetId, command.ValuationDate, command.Value, currency,
      command.ValuationMethod, command.ValuedBy, command.Notes);

    await context.AssetValuations.AddAsync(valuation, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    return Result<CreateAssetValuationCommandResult>.Success(new CreateAssetValuationCommandResult(valuation.Id.Value));
  }
}
