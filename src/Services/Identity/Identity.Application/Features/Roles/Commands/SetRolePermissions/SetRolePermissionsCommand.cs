using FluentValidation;

public sealed record SetRolePermissionsCommandResult(int AddedCount, int RemovedCount);

/// Makes a role carry exactly these permissions - the role editor's save. What it adds and removes is
/// recorded as ONE activity line, so a review reads "changed what Director can do: +… -…".
public sealed record SetRolePermissionsCommand(Guid RoleId, IReadOnlyList<Guid> PermissionIds)
  : ICommand<Result<SetRolePermissionsCommandResult>>;

public class SetRolePermissionsCommandValidator : AbstractValidator<SetRolePermissionsCommand>
{
  public SetRolePermissionsCommandValidator()
  {
    RuleFor(x => x.RoleId).NotEmpty();
    // an empty list is allowed: it takes everything away
    RuleFor(x => x.PermissionIds).NotNull();
  }
}
