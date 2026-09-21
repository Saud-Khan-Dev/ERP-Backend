using FluentValidation;

public sealed record LifecycleEventTypeInput(string? Stage, string Code, string Name, string? Description, bool IsActive = true);

public sealed record CreateLifecycleEventTypeCommandResult(Guid Id);

public sealed record CreateLifecycleEventTypeCommand(LifecycleEventTypeInput EventType) : ICommand<Result<CreateLifecycleEventTypeCommandResult>>;

public class LifecycleEventTypeInputValidator : AbstractValidator<LifecycleEventTypeInput>
{
  public LifecycleEventTypeInputValidator()
  {
    RuleFor(x => x.Stage).MaximumLength(50);
    RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
    RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
  }
}

public class CreateLifecycleEventTypeCommandValidator : AbstractValidator<CreateLifecycleEventTypeCommand>
{
  public CreateLifecycleEventTypeCommandValidator()
  {
    RuleFor(x => x.EventType).NotNull().SetValidator(new LifecycleEventTypeInputValidator());
  }
}
