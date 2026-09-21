using Microsoft.EntityFrameworkCore;

public class DisposeAssetHandler(IApplicationDbContext context)
  : ICommandHandler<DisposeAssetCommand, Result<DisposeAssetCommandResult>>
{
  public async Task<Result<DisposeAssetCommandResult>> Handle(DisposeAssetCommand command, CancellationToken cancellationToken)
  {
    var assetId = AssetId.Of(command.AssetId);
    var asset = await context.Assets.FirstOrDefaultAsync(a => a.Id == assetId, cancellationToken)
      ?? throw new AssetNotFoundException($"Asset {command.AssetId} was not found.");

    if (await context.AssetDisposals.AnyAsync(d => d.AssetId == assetId, cancellationToken))
      return Result<DisposeAssetCommandResult>.Failure("This asset has already been disposed.");

    var methodId = DisposalMethodId.Of(command.DisposalMethodId);
    var method = await context.DisposalMethods.AsNoTracking().FirstOrDefaultAsync(m => m.Id == methodId, cancellationToken)
      ?? throw new DisposalMethodNotFoundException($"Disposal method {command.DisposalMethodId} was not found.");

    var toStatusId = AssetStatusId.Of(command.ToStatusId);
    var toStatus = await context.AssetStatuses.AsNoTracking().FirstOrDefaultAsync(s => s.Id == toStatusId, cancellationToken)
      ?? throw new AssetStatusNotFoundException($"Asset status {command.ToStatusId} was not found.");

    if (!toStatus.IsTerminal)
      return Result<DisposeAssetCommandResult>.Failure($"Status '{toStatus.Name.Value}' is not a terminal status; disposals must end in a terminal status.");

    var eventTypeId = LifecycleEventTypeId.Of(command.EventTypeId);
    var eventType = await context.LifecycleEventTypes.AsNoTracking().FirstOrDefaultAsync(t => t.Id == eventTypeId, cancellationToken)
      ?? throw new LifecycleEventTypeNotFoundException($"Lifecycle event type {command.EventTypeId} was not found.");

    Currency? currency = null;
    if (command.CurrencyCode is not null)
    {
      currency = Currency.Of(command.CurrencyCode);
      if (!await context.Currencies.AnyAsync(c => c.Id == currency && c.IsActive, cancellationToken))
        throw new CurrencyNotFoundException($"Currency {currency.Value} was not found or is inactive.");
    }

    // net book value snapshot = acquisition cost - posted depreciation on the active schedule
    var acquisition = await context.AssetAcquisitions.AsNoTracking().FirstOrDefaultAsync(a => a.AssetId == assetId, cancellationToken);
    var activeSchedule = await context.AssetDepreciationSchedules.Include(s => s.Entries)
      .FirstOrDefaultAsync(s => s.AssetId == assetId && s.IsActive, cancellationToken);

    decimal? netBookValue = acquisition is null
      ? null
      : activeSchedule?.NetBookValue(acquisition.AcquisitionCost) ?? acquisition.AcquisitionCost;

    var disposal = AssetDisposal.Create(
      AssetDisposalId.Of(Guid.NewGuid()), assetId, method, command.DisposalDate, command.DisposalValue, currency,
      netBookValue, command.BuyerInfo, command.Reason, command.ApprovedBy, command.ApprovedAt);

    activeSchedule?.Deactivate(command.DisposalDate);

    var fromStatus = await context.AssetStatuses.AsNoTracking().FirstAsync(s => s.Id == asset.StatusId, cancellationToken);
    asset.ChangeStatus(fromStatus, toStatus, hasActiveDepreciationSchedule: false);

    var lifecycleEvent = AssetLifecycleEvent.Create(
      AssetLifecycleEventId.Of(Guid.NewGuid()), assetId, eventType,
      command.DisposalDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
      fromStatus.Id, toStatus.Id, command.PerformedBy, command.Reason, details: null);

    await context.AssetDisposals.AddAsync(disposal, cancellationToken);
    await context.AssetLifecycleEvents.AddAsync(lifecycleEvent, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    return Result<DisposeAssetCommandResult>.Success(new DisposeAssetCommandResult(disposal.Id.Value));
  }
}
