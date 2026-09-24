using FluentValidation;

public sealed record RevokeUserSessionsCommandResult(int RevokedCount);

/// Forced logout: signs a user out of every device.
public sealed record RevokeUserSessionsCommand(Guid UserId) : ICommand<Result<RevokeUserSessionsCommandResult>>;

public class RevokeUserSessionsCommandValidator : AbstractValidator<RevokeUserSessionsCommand>
{
  public RevokeUserSessionsCommandValidator()
  {
    RuleFor(x => x.UserId).NotEmpty();
  }
}
