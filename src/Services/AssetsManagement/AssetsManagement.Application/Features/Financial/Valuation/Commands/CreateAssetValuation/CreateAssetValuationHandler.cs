using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

public class CreateAssetValuationHandler(IApplicationDbContext context, ICurrentUser currentUser, IClock clock)
  : ICommandHandler<CreateAssetValuationCommand, Result<CreateAssetValuationCommandResult>>
{
  public async Task<Result<CreateAssetValuationCommandResult>> Handle(CreateAssetValuationCommand command, CancellationToken cancellationToken)
  {
    var assetId = AssetId.Of(command.AssetId);
    var asset = await AssetRecordGuard.LoadAsync(context, assetId, track: false, cancellationToken);
    await AssetRecordGuard.EnsureOpenAsync(context, asset, cancellationToken);

    var currency = Currency.Of(command.CurrencyCode);
    if (!await context.Currencies.AnyAsync(c => c.Id == currency && c.IsActive, cancellationToken))
      throw new CurrencyNotFoundException($"The currency {currency.Value} was not found or is switched off.");

    if (await context.AssetValuations.AnyAsync(v => v.AssetId == assetId && v.ValuationDate == command.ValuationDate, cancellationToken))
      return Result<CreateAssetValuationCommandResult>.Failure($"The asset already has a valuation on {command.ValuationDate:dd'/'MM'/'yyyy}.");

    var actor = currentUser.Actor();
    var valuation = AssetValuation.Create(
      AssetValuationId.Of(Guid.NewGuid()), assetId, command.ValuationDate, command.Value, currency,
      command.ValuationMethod, command.ValuedByName, command.Notes, actor, clock.Today);

    await context.AssetValuations.AddAsync(valuation, cancellationToken);

    var details = new Dictionary<string, string> { ["Value"] = $"{command.Value.ToString("N2", CultureInfo.InvariantCulture)} {currency.Value}" };
    if (!string.IsNullOrWhiteSpace(command.ValuationMethod)) details["Method"] = command.ValuationMethod.Trim();
    if (!string.IsNullOrWhiteSpace(command.ValuedByName)) details["Valued by"] = command.ValuedByName.Trim();
    var at = command.ValuationDate == clock.Today ? clock.UtcNow : command.ValuationDate.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Utc);
    await SystemEvents.AddAsync(context, assetId, EventPurpose.Valuation, at, actor, command.Notes, JsonSerializer.SerializeToElement(details), cancellationToken: cancellationToken);

    await context.SaveChangesAsync(cancellationToken);

    return Result<CreateAssetValuationCommandResult>.Success(new CreateAssetValuationCommandResult(valuation.Id.Value));
  }
}
