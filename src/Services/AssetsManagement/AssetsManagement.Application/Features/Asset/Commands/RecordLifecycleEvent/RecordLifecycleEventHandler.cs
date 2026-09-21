using Microsoft.EntityFrameworkCore;

public class RecordLifecycleEventHandler(IApplicationDbContext context)
  : ICommandHandler<RecordLifecycleEventCommand, Result<RecordLifecycleEventCommandResult>>
{
  public async Task<Result<RecordLifecycleEventCommandResult>> Handle(RecordLifecycleEventCommand command, CancellationToken cancellationToken)
  {
    var assetId = AssetId.Of(command.AssetId);
    if (!await context.Assets.AnyAsync(a => a.Id == assetId, cancellationToken))
      throw new AssetNotFoundException($"Asset {command.AssetId} was not found.");

    var eventTypeId = LifecycleEventTypeId.Of(command.EventTypeId);
    var eventType = await context.LifecycleEventTypes.AsNoTracking().FirstOrDefaultAsync(t => t.Id == eventTypeId, cancellationToken)
      ?? throw new LifecycleEventTypeNotFoundException($"Lifecycle event type {command.EventTypeId} was not found.");

    var lifecycleEvent = AssetLifecycleEvent.Create(
      AssetLifecycleEventId.Of(Guid.NewGuid()), assetId, eventType, command.EventDate ?? DateTime.UtcNow,
      null, null, command.PerformedBy, command.Notes, command.Details);

    await context.AssetLifecycleEvents.AddAsync(lifecycleEvent, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    return Result<RecordLifecycleEventCommandResult>.Success(new RecordLifecycleEventCommandResult(lifecycleEvent.Id.Value));
  }
}
