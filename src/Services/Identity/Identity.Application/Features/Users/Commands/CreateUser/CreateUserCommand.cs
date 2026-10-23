using FluentValidation;

/// Note what is absent: no `role` or `isSuperAdmin` string, no password hash, no "isAdmin" flag.
/// Roles are referenced by id and every one of them is checked server-side before it is granted.
public sealed record CreateUserInput(
  string Username,
  string Email,
  string DisplayName,
  /// The person's HR record (HRM's staff directory), with HR's employee number as `EmployeeCode`. Leave both null for
  /// an account that is not linked yet (or not an employee) - Identity issues no codes of its own.
  Guid? EmployeeId = null,
  /// Leave null to have the server generate one and return it once.
  string? TemporaryPassword = null,
  IReadOnlyList<Guid>? RoleIds = null,
  /// Ignored: a new account always changes its first password (kept so older callers still validate).
  bool MustChangePassword = true,
  string? EmployeeCode = null,
  /// Ignored (Identity no longer issues employee codes).
  bool AutoGenerateEmployeeCode = false,
  /// Why the person may hold two duties that should be kept apart (needed only when the roles put such a pair together).
  string? ConflictReason = null);

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
    RuleFor(x => x.EmployeeCode).NotEmpty().When(x => x.EmployeeId is not null)
      .WithMessage("The HR employee number is missing - choose the person again.");
    RuleFor(x => x.EmployeeId).Must(id => id != Guid.Empty).WithMessage("Choose the person's HR record.");
  }
}

public class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
  public CreateUserCommandValidator()
  {
    RuleFor(x => x.User).NotNull().SetValidator(new CreateUserInputValidator());
  }
}
