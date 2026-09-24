using FluentValidation;

public sealed record RevokeMySessionCommandResult(bool IsSuccess);

/// Sign out one of my other devices.
public sealed record RevokeMySessionCommand(Guid SessionId) : ICommand<Result<RevokeMySessionCommandResult>>;

public class RevokeMySessionCommandValidator : AbstractValidator<RevokeMySessionCommand>
{
  public RevokeMySessionCommandValidator()
  {
    RuleFor(x => x.SessionId).NotEmpty();
  }
}
