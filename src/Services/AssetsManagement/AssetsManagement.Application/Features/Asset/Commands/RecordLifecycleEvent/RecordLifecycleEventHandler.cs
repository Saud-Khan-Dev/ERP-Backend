using Microsoft.EntityFrameworkCore;

public class RecordLifecycleEventHandler(IApplicationDbContext context, ICurrentUser currentUser, IClock clock)
  : ICommandHandler<RecordLifecycleEventCommand, Result<RecordLifecycleEventCommandResult>>
{
  public async Task<Result<RecordLifecycleEventCommandResult>> Handle(RecordLifecycleEventCommand command, CancellationToken cancellationToken)
  {
    var assetId = AssetId.Of(command.AssetId);
    var asset = await AssetRecordGuard.LoadAsync(context, assetId, track: false, cancellationToken);
    await AssetRecordGuard.EnsureOpenAsync(context, asset, cancellationToken);

    var eventTypeId = LifecycleEventTypeId.Of(command.EventTypeId);
    var eventType = await context.LifecycleEventTypes.AsNoTracking().FirstOrDefaultAsync(t => t.Id == eventTypeId, cancellationToken)
      ?? throw new LifecycleEventTypeNotFoundException("That kind of event was not found. Choose it again.");

    if (!eventType.IsHandRecorded)
      return Result<RecordLifecycleEventCommandResult>.Failure($"'{eventType.Name.Value}' is written by its own act (transfer, status change, disposal …), not recorded by hand.");

    var purchaseDate = await context.AssetAcquisitions.AsNoTracking().Where(a => a.AssetId == assetId).Select(a => (DateOnly?)a.AcquisitionDate).FirstOrDefaultAsync(cancellationToken);
    var eventDate = EventDates.Check(command.EventDate, clock, asset, purchaseDate);

    var lifecycleEvent = AssetLifecycleEvent.Create(
      AssetLifecycleEventId.Of(Guid.NewGuid()), assetId, eventType, eventDate, null, null, currentUser.Actor(), command.Notes, command.Details);

    await context.AssetLifecycleEvents.AddAsync(lifecycleEvent, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    return Result<RecordLifecycleEventCommandResult>.Success(new RecordLifecycleEventCommandResult(lifecycleEvent.Id.Value));
  }
}
