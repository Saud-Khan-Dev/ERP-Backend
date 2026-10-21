using Microsoft.EntityFrameworkCore;

public class SetRolePermissionsHandler(IApplicationDbContext context, IdentityGuard guard, ICurrentUser currentUser,
    IActivityRecorder activity)
  : ICommandHandler<SetRolePermissionsCommand, Result<SetRolePermissionsCommandResult>>
{
  public async Task<Result<SetRolePermissionsCommandResult>> Handle(SetRolePermissionsCommand command, CancellationToken cancellationToken)
  {
    var now = DateTime.UtcNow;
    var roleId = RoleId.Of(command.RoleId);

    var role = await context.Roles.AsNoTracking().FirstOrDefaultAsync(r => r.Id == roleId, cancellationToken)
      ?? throw new RoleNotFoundException("This role was not found.");

    guard.EnsureCanAdministerRole(role);
    await guard.EnsureNotOwnRoleAsync(role.Id, cancellationToken);

    var wanted = command.PermissionIds.Distinct().Select(PermissionId.Of).ToList();
    var found = await context.Permissions.CountAsync(p => wanted.Contains(p.Id), cancellationToken);

    if (found != wanted.Count)
      throw new PermissionNotFoundException("One or more of the supplied permissions was not found.");

    var links = await context.RolePermissions.Where(rp => rp.RoleId == roleId).ToListAsync(cancellationToken);
    var held = links.Select(rp => rp.PermissionId).ToList();

    var toAdd = wanted.Except(held).ToList();
    var toRemove = links.Where(rp => !wanted.Contains(rp.PermissionId)).ToList();

    await guard.EnsureCanGrantAsync(toAdd, $"give {role.RoleName.Value} this access", cancellationToken);

    // stripping SUPER_ADMIN's own administration permissions would lock everyone out of the system
    if (role.IsSuperAdmin && toRemove.Count > 0)
      return Result<SetRolePermissionsCommandResult>.Failure(
        "Nothing can be taken away from the built-in Super Administrator role.");

    if (toAdd.Count == 0 && toRemove.Count == 0)
      return Result<SetRolePermissionsCommandResult>.Success(new SetRolePermissionsCommandResult(0, 0));

    foreach (var permissionId in toAdd)
      await context.RolePermissions.AddAsync(
        RolePermission.Create(roleId, permissionId, currentUser.UserId, now), cancellationToken);

    context.RolePermissions.RemoveRange(toRemove);

    var detail = await RolePermissionDetail.DescribeAsync(context, toAdd, toRemove.Select(rp => rp.PermissionId), cancellationToken);
    await activity.RecordAsync(ActivityAction.RolePermissionsChanged, ActivityTargetType.Role, role.Id.Value, role.RoleName.Value,
      detail, cancellationToken);

    await context.SaveChangesAsync(cancellationToken);

    return Result<SetRolePermissionsCommandResult>.Success(new SetRolePermissionsCommandResult(toAdd.Count, toRemove.Count));
  }
}
