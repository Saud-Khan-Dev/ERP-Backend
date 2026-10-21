using Microsoft.EntityFrameworkCore;

public class DeleteLifecycleEventTypeHandler(IApplicationDbContext context)
  : ICommandHandler<DeleteLifecycleEventTypeCommand, Result<DeleteLifecycleEventTypeCommandResult>>
{
  public async Task<Result<DeleteLifecycleEventTypeCommandResult>> Handle(DeleteLifecycleEventTypeCommand command, CancellationToken cancellationToken)
  {
    var id = LifecycleEventTypeId.Of(command.Id);
    var eventType = await context.LifecycleEventTypes.FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
      ?? throw new LifecycleEventTypeNotFoundException($"Lifecycle event type {command.Id} was not found.");

    eventType.EnsureDeletable();

    if (await context.AssetLifecycleEvents.AnyAsync(e => e.EventTypeId == id, cancellationToken))
      return Result<DeleteLifecycleEventTypeCommandResult>.Failure($"'{eventType.Name.Value}' is on asset timelines, so it cannot be deleted. Switch it off instead.");

    context.LifecycleEventTypes.Remove(eventType);
    await context.SaveChangesAsync(cancellationToken);

    return Result<DeleteLifecycleEventTypeCommandResult>.Success(new DeleteLifecycleEventTypeCommandResult(true));
  }
}
