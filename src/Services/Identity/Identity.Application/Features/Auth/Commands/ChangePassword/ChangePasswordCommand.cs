using FluentValidation;

public sealed record ChangePasswordCommandResult(bool IsSuccess);

/// The caller changes their *own* password. The user id comes from the token, never from the body.
public sealed record ChangePasswordCommand(string CurrentPassword, string NewPassword)
  : ICommand<Result<ChangePasswordCommandResult>>;

public class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
  public ChangePasswordCommandValidator()
  {
    RuleFor(x => x.CurrentPassword).NotEmpty().WithMessage("Your current password is required.");
    RuleFor(x => x.NewPassword).NotEmpty().WithMessage("A new password is required.");
    RuleFor(x => x.NewPassword)
      .NotEqual(x => x.CurrentPassword)
      .WithMessage("The new password must be different from the current one.");
  }
}
