using Microsoft.EntityFrameworkCore;

public class RemoveRolePermissionHandler(IApplicationDbContext context, IdentityGuard guard,
    IActivityRecorder activity)
  : ICommandHandler<RemoveRolePermissionCommand, Result<RemoveRolePermissionCommandResult>>
{
  public async Task<Result<RemoveRolePermissionCommandResult>> Handle(RemoveRolePermissionCommand command, CancellationToken cancellationToken)
  {
    var roleId = RoleId.Of(command.RoleId);
    var permissionId = PermissionId.Of(command.PermissionId);

    var role = await context.Roles.AsNoTracking().FirstOrDefaultAsync(r => r.Id == roleId, cancellationToken)
      ?? throw new RoleNotFoundException($"Role {command.RoleId} was not found.");

    guard.EnsureCanAdministerRole(role);

    // stripping SUPER_ADMIN's own administration permissions would lock everyone out of the system
    if (role.IsSuperAdmin)
      return Result<RemoveRolePermissionCommandResult>.Failure(
        $"Permissions cannot be removed from the built-in {Role.SuperAdminCode} role.");

    var link = await context.RolePermissions
        .FirstOrDefaultAsync(rp => rp.RoleId == roleId && rp.PermissionId == permissionId, cancellationToken)
      ?? throw new PermissionNotFoundException("That permission is not attached to this role.");

    context.RolePermissions.Remove(link);
    await activity.RecordAsync(ActivityAction.RolePermissionRemoved, ActivityTargetType.Role, role.Id.Value, role.RoleName.Value, null, cancellationToken);

    await context.SaveChangesAsync(cancellationToken);

    return Result<RemoveRolePermissionCommandResult>.Success(new RemoveRolePermissionCommandResult(true));
  }
}
