using Microsoft.EntityFrameworkCore;

public class UpdateLifecycleEventTypeHandler(IApplicationDbContext context)
  : ICommandHandler<UpdateLifecycleEventTypeCommand, Result<UpdateLifecycleEventTypeCommandResult>>
{
  public async Task<Result<UpdateLifecycleEventTypeCommandResult>> Handle(UpdateLifecycleEventTypeCommand command, CancellationToken cancellationToken)
  {
    var id = LifecycleEventTypeId.Of(command.Id);
    var eventType = await context.LifecycleEventTypes.FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
      ?? throw new LifecycleEventTypeNotFoundException($"Lifecycle event type {command.Id} was not found.");

    var input = command.EventType;
    var code = LookupCode.Of(input.Code);

    if (await context.LifecycleEventTypes.AnyAsync(t => t.Id != id && t.Code == code, cancellationToken))
      return Result<UpdateLifecycleEventTypeCommandResult>.Failure($"A lifecycle event type with code {code.Value} already exists.");

    eventType.Update(input.Stage, code, Name.Of(input.Name, 100), input.Description, input.IsActive);
    await context.SaveChangesAsync(cancellationToken);

    return Result<UpdateLifecycleEventTypeCommandResult>.Success(new UpdateLifecycleEventTypeCommandResult(true));
  }
}
