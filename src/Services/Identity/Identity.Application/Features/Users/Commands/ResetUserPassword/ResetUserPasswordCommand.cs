using FluentValidation;

public sealed record ResetUserPasswordCommandResult(string? GeneratedPassword);

/// Administrative reset. The administrator never learns the old password, and the new one must be
/// changed by the employee at next sign-in.
public sealed record ResetUserPasswordCommand(Guid Id, string? NewPassword = null)
  : ICommand<Result<ResetUserPasswordCommandResult>>;

public class ResetUserPasswordCommandValidator : AbstractValidator<ResetUserPasswordCommand>
{
  public ResetUserPasswordCommandValidator()
  {
    RuleFor(x => x.Id).NotEmpty();
  }
}
