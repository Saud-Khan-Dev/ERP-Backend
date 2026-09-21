using FluentValidation;

public sealed record UpdateLifecycleEventTypeCommandResult(bool IsSuccess);

public sealed record UpdateLifecycleEventTypeCommand(Guid Id, LifecycleEventTypeInput EventType) : ICommand<Result<UpdateLifecycleEventTypeCommandResult>>;

public class UpdateLifecycleEventTypeCommandValidator : AbstractValidator<UpdateLifecycleEventTypeCommand>
{
  public UpdateLifecycleEventTypeCommandValidator()
  {
    RuleFor(x => x.Id).NotEmpty();
    RuleFor(x => x.EventType).NotNull().SetValidator(new LifecycleEventTypeInputValidator());
  }
}
