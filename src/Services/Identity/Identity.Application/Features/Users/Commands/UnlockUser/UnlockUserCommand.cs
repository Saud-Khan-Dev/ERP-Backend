using FluentValidation;

public sealed record UnlockUserCommandResult(bool IsSuccess);

/// Clears a brute-force lockout without touching the password.
public sealed record UnlockUserCommand(Guid Id) : ICommand<Result<UnlockUserCommandResult>>;

public class UnlockUserCommandValidator : AbstractValidator<UnlockUserCommand>
{
  public UnlockUserCommandValidator()
  {
    RuleFor(x => x.Id).NotEmpty();
  }
}
