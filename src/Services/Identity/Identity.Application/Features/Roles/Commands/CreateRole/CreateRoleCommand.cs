using FluentValidation;

public sealed record RoleInput(string Code, string Name, string? Description = null, IReadOnlyList<Guid>? PermissionIds = null);

public sealed record CreateRoleCommandResult(Guid Id);

public sealed record CreateRoleCommand(RoleInput Role) : ICommand<Result<CreateRoleCommandResult>>;

public class RoleInputValidator : AbstractValidator<RoleInput>
{
  public RoleInputValidator()
  {
    RuleFor(x => x.Code).NotEmpty().MaximumLength(LookupCode.MaxLength);
    RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    RuleFor(x => x.Description).MaximumLength(1000);
  }
}

public class CreateRoleCommandValidator : AbstractValidator<CreateRoleCommand>
{
  public CreateRoleCommandValidator()
  {
    RuleFor(x => x.Role).NotNull().SetValidator(new RoleInputValidator());
  }
}
