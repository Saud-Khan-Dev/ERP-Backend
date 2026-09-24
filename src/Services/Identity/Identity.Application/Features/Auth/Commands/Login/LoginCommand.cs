using FluentValidation;

public sealed record LoginCommandResult(AuthenticationResultDto Authentication);

/// IpAddress and UserAgent are filled in by the endpoint from the HTTP context — never by the
/// client — because they are written to the audit trail.
public sealed record LoginCommand(string Username, string Password, string? IpAddress, string? UserAgent)
  : ICommand<Result<LoginCommandResult>>;

public class LoginCommandValidator : AbstractValidator<LoginCommand>
{
  public LoginCommandValidator()
  {
    RuleFor(x => x.Username).NotEmpty().WithMessage("Username is required.");
    RuleFor(x => x.Password).NotEmpty().WithMessage("Password is required.");
  }
}
