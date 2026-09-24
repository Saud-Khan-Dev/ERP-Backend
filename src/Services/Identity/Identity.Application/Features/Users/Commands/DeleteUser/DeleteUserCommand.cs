using FluentValidation;

public sealed record DeleteUserCommandResult(bool IsSuccess);

/// Soft delete — the row stays so login history and "who changed this" references remain resolvable.
public sealed record DeleteUserCommand(Guid Id) : ICommand<Result<DeleteUserCommandResult>>;

public class DeleteUserCommandValidator : AbstractValidator<DeleteUserCommand>
{
  public DeleteUserCommandValidator()
  {
    RuleFor(x => x.Id).NotEmpty();
  }
}
