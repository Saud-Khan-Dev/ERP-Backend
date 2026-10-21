using FluentValidation;

/// Name and email. Roles, activation, passwords and the HR link (which brings the employee code) each have their own
/// command so that every change of access or identity is an explicit, separately-authorized act. EmployeeId and
/// EmployeeCode are accepted from older callers and ignored.
public sealed record UpdateUserInput(string Email, string DisplayName, Guid? EmployeeId = null, string? EmployeeCode = null);

public sealed record UpdateUserCommandResult(bool IsSuccess);

public sealed record UpdateUserCommand(Guid Id, UpdateUserInput User) : ICommand<Result<UpdateUserCommandResult>>;

public class UpdateUserInputValidator : AbstractValidator<UpdateUserInput>
{
  public UpdateUserInputValidator()
  {
    RuleFor(x => x.Email).NotEmpty().MaximumLength(EmailAddress.MaxLength);
    RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(150);
  }
}

public class UpdateUserCommandValidator : AbstractValidator<UpdateUserCommand>
{
  public UpdateUserCommandValidator()
  {
    RuleFor(x => x.Id).NotEmpty();
    RuleFor(x => x.User).NotNull().SetValidator(new UpdateUserInputValidator());
  }
}
