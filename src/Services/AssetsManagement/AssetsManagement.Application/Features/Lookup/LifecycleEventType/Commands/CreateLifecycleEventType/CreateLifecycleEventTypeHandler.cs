using Microsoft.EntityFrameworkCore;

public class CreateLifecycleEventTypeHandler(IApplicationDbContext context)
  : ICommandHandler<CreateLifecycleEventTypeCommand, Result<CreateLifecycleEventTypeCommandResult>>
{
  public async Task<Result<CreateLifecycleEventTypeCommandResult>> Handle(CreateLifecycleEventTypeCommand command, CancellationToken cancellationToken)
  {
    var input = command.EventType;
    var code = LookupCode.Of(input.Code);

    if (await context.LifecycleEventTypes.AnyAsync(t => t.Code == code, cancellationToken))
      return Result<CreateLifecycleEventTypeCommandResult>.Failure($"A lifecycle event type with code {code.Value} already exists.");

    var eventType = LifecycleEventType.Create(LifecycleEventTypeId.Of(Guid.NewGuid()), input.Stage, code, Name.Of(input.Name, 100), input.Description);

    if (!input.IsActive)
      eventType.Update(input.Stage, code, Name.Of(input.Name, 100), input.Description, false);

    await context.LifecycleEventTypes.AddAsync(eventType, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    return Result<CreateLifecycleEventTypeCommandResult>.Success(new CreateLifecycleEventTypeCommandResult(eventType.Id.Value));
  }
}
