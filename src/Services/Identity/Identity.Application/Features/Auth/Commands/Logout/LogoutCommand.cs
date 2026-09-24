using FluentValidation;

public sealed record LogoutCommandResult(bool IsSuccess);

/// Revokes the session behind the supplied refresh token. Set AllSessions to sign out every device.
public sealed record LogoutCommand(string RefreshToken, bool AllSessions = false)
  : ICommand<Result<LogoutCommandResult>>;

public class LogoutCommandValidator : AbstractValidator<LogoutCommand>
{
  public LogoutCommandValidator()
  {
    RuleFor(x => x.RefreshToken).NotEmpty().WithMessage("A refresh token is required.");
  }
}
