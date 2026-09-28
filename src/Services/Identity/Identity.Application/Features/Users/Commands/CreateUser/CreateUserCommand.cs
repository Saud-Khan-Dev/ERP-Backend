using FluentValidation;

/// Note what is absent: no `role` or `isSuperAdmin` string, no password hash, no "isAdmin" flag.
/// Roles are referenced by id and every one of them is checked server-side before it is granted.
public sealed record CreateUserInput(
  string Username,
  string Email,
  string DisplayName,
  Guid? EmployeeId = null,
  /// Leave null to have the server generate one and return it once.
  string? TemporaryPassword = null,
  IReadOnlyList<Guid>? RoleIds = null,
  bool MustChangePassword = true,
  /// e.g. EMP-205. Must follow the employee code template. Leave null to have the next code issued.
  string? EmployeeCode = null,
  /// Set false (with no EmployeeCode) for an account that is not an employee, such as a service account.
  bool AutoGenerateEmployeeCode = true);

/// GeneratedPassword is populated only when the server generated it — it is shown to the
/// administrator once and never stored in plaintext or retrievable again.
public sealed record CreateUserCommandResult(Guid Id, string? GeneratedPassword, string? EmployeeCode);

public sealed record CreateUserCommand(CreateUserInput User) : ICommand<Result<CreateUserCommandResult>>;

public class CreateUserInputValidator : AbstractValidator<CreateUserInput>
{
  public CreateUserInputValidator()
  {
    RuleFor(x => x.Username).NotEmpty().MaximumLength(Username.MaxLength);
    RuleFor(x => x.Email).NotEmpty().MaximumLength(EmailAddress.MaxLength);
    RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(150);
    RuleForEach(x => x.RoleIds).NotEmpty().WithMessage("Role ids cannot be empty.");
    RuleFor(x => x.EmployeeCode).MaximumLength(EmployeeCode.MaxLength);
  }
}

public class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
  public CreateUserCommandValidator()
  {
    RuleFor(x => x.User).NotNull().SetValidator(new CreateUserInputValidator());
  }
}
