using FluentValidation;

public sealed record SetUserActivationCommandResult(bool IsActive);

/// Activate or deactivate an account. Deactivating also revokes every live session, so access stops
/// immediately rather than when the current access token happens to expire.
public sealed record SetUserActivationCommand(Guid Id, bool IsActive) : ICommand<Result<SetUserActivationCommandResult>>;

public class SetUserActivationCommandValidator : AbstractValidator<SetUserActivationCommand>
{
  public SetUserActivationCommandValidator()
  {
    RuleFor(x => x.Id).NotEmpty();
  }
}
