using FluentValidation;

public sealed record DeleteLifecycleEventTypeCommandResult(bool IsSuccess);

public sealed record DeleteLifecycleEventTypeCommand(Guid Id) : ICommand<Result<DeleteLifecycleEventTypeCommandResult>>;

public class DeleteLifecycleEventTypeCommandValidator : AbstractValidator<DeleteLifecycleEventTypeCommand>
{
  public DeleteLifecycleEventTypeCommandValidator()
  {
    RuleFor(x => x.Id).NotEmpty();
  }
}
