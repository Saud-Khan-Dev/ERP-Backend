using FluentValidation;

public sealed record AddRolePermissionsCommandResult(int AddedCount);

/// Attaches one or more permissions to a role. Already-attached permissions are ignored, so the
/// call is safe to repeat.
public sealed record AddRolePermissionsCommand(Guid RoleId, IReadOnlyList<Guid> PermissionIds)
  : ICommand<Result<AddRolePermissionsCommandResult>>;

public class AddRolePermissionsCommandValidator : AbstractValidator<AddRolePermissionsCommand>
{
  public AddRolePermissionsCommandValidator()
  {
    RuleFor(x => x.RoleId).NotEmpty();
    RuleFor(x => x.PermissionIds).NotEmpty().WithMessage("At least one permission is required.");
  }
}
