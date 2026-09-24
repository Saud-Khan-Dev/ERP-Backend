using FluentValidation;

public sealed record RefreshTokenCommandResult(AuthenticationResultDto Authentication);

public sealed record RefreshTokenCommand(string RefreshToken, string? IpAddress, string? UserAgent)
  : ICommand<Result<RefreshTokenCommandResult>>;

public class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
  public RefreshTokenCommandValidator()
  {
    RuleFor(x => x.RefreshToken).NotEmpty().WithMessage("A refresh token is required.");
  }
}
