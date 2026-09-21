using Microsoft.EntityFrameworkCore;

public class ChangeAssetStatusHandler(IApplicationDbContext context)
  : ICommandHandler<ChangeAssetStatusCommand, Result<ChangeAssetStatusCommandResult>>
{
  public async Task<Result<ChangeAssetStatusCommandResult>> Handle(ChangeAssetStatusCommand command, CancellationToken cancellationToken)
  {
    var assetId = AssetId.Of(command.AssetId);
    var asset = await context.Assets.FirstOrDefaultAsync(a => a.Id == assetId, cancellationToken)
      ?? throw new AssetNotFoundException($"Asset {command.AssetId} was not found.");

    var fromStatus = await context.AssetStatuses.AsNoTracking().FirstAsync(s => s.Id == asset.StatusId, cancellationToken);

    var toStatusId = AssetStatusId.Of(command.ToStatusId);
    var toStatus = await context.AssetStatuses.AsNoTracking().FirstOrDefaultAsync(s => s.Id == toStatusId, cancellationToken)
      ?? throw new AssetStatusNotFoundException($"Asset status {command.ToStatusId} was not found.");

    var eventTypeId = LifecycleEventTypeId.Of(command.EventTypeId);
    var eventType = await context.LifecycleEventTypes.AsNoTracking().FirstOrDefaultAsync(t => t.Id == eventTypeId, cancellationToken)
      ?? throw new LifecycleEventTypeNotFoundException($"Lifecycle event type {command.EventTypeId} was not found.");

    if (fromStatus.Id == toStatus.Id)
      return Result<ChangeAssetStatusCommandResult>.Failure($"Asset is already in status '{toStatus.Name.Value}'.");

    var hasActiveSchedule = await context.AssetDepreciationSchedules.AnyAsync(s => s.AssetId == assetId && s.IsActive, cancellationToken);

    asset.ChangeStatus(fromStatus, toStatus, hasActiveSchedule);

    var lifecycleEvent = AssetLifecycleEvent.Create(
      AssetLifecycleEventId.Of(Guid.NewGuid()),
      assetId,
      eventType,
      command.EventDate ?? DateTime.UtcNow,
      fromStatus.Id,
      toStatus.Id,
      command.PerformedBy,
      command.Notes,
      details: null);

    await context.AssetLifecycleEvents.AddAsync(lifecycleEvent, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    return Result<ChangeAssetStatusCommandResult>.Success(new ChangeAssetStatusCommandResult(lifecycleEvent.Id.Value));
  }
}
