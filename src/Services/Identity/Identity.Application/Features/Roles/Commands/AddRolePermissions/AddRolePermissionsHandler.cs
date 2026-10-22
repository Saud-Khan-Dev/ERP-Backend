using Microsoft.EntityFrameworkCore;

public class AddRolePermissionsHandler(IApplicationDbContext context, IdentityGuard guard, ICurrentUser currentUser,
    IActivityRecorder activity)
  : ICommandHandler<AddRolePermissionsCommand, Result<AddRolePermissionsCommandResult>>
{
  public async Task<Result<AddRolePermissionsCommandResult>> Handle(AddRolePermissionsCommand command, CancellationToken cancellationToken)
  {
    var now = DateTime.UtcNow;
    var roleId = RoleId.Of(command.RoleId);

    var role = await context.Roles.AsNoTracking().FirstOrDefaultAsync(r => r.Id == roleId, cancellationToken)
      ?? throw new RoleNotFoundException("This role was not found.");

    // only a Super Admin may change what SUPER_ADMIN can do; nobody rewrites a role they hold
    guard.EnsureCanAdministerRole(role);
    await guard.EnsureNotOwnRoleAsync(role.Id, cancellationToken);

    var ids = command.PermissionIds.Distinct().Select(PermissionId.Of).ToList();
    var found = await context.Permissions.CountAsync(p => ids.Contains(p.Id), cancellationToken);

    if (found != ids.Count)
      throw new PermissionNotFoundException("One or more of the supplied permissions was not found.");

    var existing = await context.RolePermissions
        .Where(rp => rp.RoleId == roleId && ids.Contains(rp.PermissionId))
        .Select(rp => rp.PermissionId)
        .ToListAsync(cancellationToken);

    var toAdd = ids.Except(existing).ToList();
    await guard.EnsureCanGrantAsync(toAdd, $"give {role.RoleName.Value} this access", cancellationToken);

    foreach (var permissionId in toAdd)
      await context.RolePermissions.AddAsync(
        RolePermission.Create(roleId, permissionId, currentUser.UserId, now), cancellationToken);

    if (toAdd.Count > 0)
      await activity.RecordAsync(ActivityAction.RolePermissionsAdded, ActivityTargetType.Role, role.Id.Value, role.RoleName.Value,
        await RolePermissionDetail.DescribeAsync(context, toAdd, [], cancellationToken), cancellationToken);

    // every holder's tokens issued before this change stop working at once
    await context.HoldersAccessChangedAsync(roleId, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    return Result<AddRolePermissionsCommandResult>.Success(new AddRolePermissionsCommandResult(toAdd.Count));
  }
}
