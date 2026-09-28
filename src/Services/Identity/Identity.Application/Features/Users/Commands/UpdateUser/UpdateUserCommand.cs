using FluentValidation;

/// Profile fields only. Roles, activation and passwords each have their own command so that every
/// privilege change is an explicit, separately-authorized action.
/// EmployeeCode is editable: an unchanged code is kept, a new one must follow the employee code
/// template, and null clears it.
public sealed record UpdateUserInput(string Email, string DisplayName, Guid? EmployeeId = null, string? EmployeeCode = null);

public sealed record UpdateUserCommandResult(bool IsSuccess);

public sealed record UpdateUserCommand(Guid Id, UpdateUserInput User) : ICommand<Result<UpdateUserCommandResult>>;

public class UpdateUserInputValidator : AbstractValidator<UpdateUserInput>
{
  public UpdateUserInputValidator()
  {
    RuleFor(x => x.Email).NotEmpty().MaximumLength(EmailAddress.MaxLength);
    RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(150);
    RuleFor(x => x.EmployeeCode).MaximumLength(EmployeeCode.MaxLength);
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
