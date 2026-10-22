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

    // two duties that should be kept apart, newly put together in one role: say why, or the save is refused
    if (!role.IsSuperAdmin)
    {
      var codes = await context.Permissions.AsNoTracking().Where(p => wanted.Contains(p.Id) || held.Contains(p.Id))
        .Select(p => new { p.Id, Code = p.Code.Value }).ToListAsync(cancellationToken);
      var before = codes.Where(c => held.Contains(c.Id)).Select(c => c.Code).ToList();
      var after = codes.Where(c => wanted.Contains(c.Id)).Select(c => c.Code).ToList();
      var known = AccessMap.ConflictsIn(before);
      if (AccessMap.ConflictsIn(after).Any(c => !known.Contains(c)))
        await ConflictCheck.EnsureAcceptedAsync(activity, after, command.ConflictReason,
          ActivityTargetType.Role, role.Id.Value, role.RoleName.Value, cancellationToken);
    }

    foreach (var permissionId in toAdd)
      await context.RolePermissions.AddAsync(
        RolePermission.Create(roleId, permissionId, currentUser.UserId, now), cancellationToken);

    context.RolePermissions.RemoveRange(toRemove);

    var detail = await RolePermissionDetail.DescribeAsync(context, toAdd, toRemove.Select(rp => rp.PermissionId), cancellationToken);
    await activity.RecordAsync(ActivityAction.RolePermissionsChanged, ActivityTargetType.Role, role.Id.Value, role.RoleName.Value,
      detail, cancellationToken);

    // every holder's tokens issued before this change stop working at once
    await context.HoldersAccessChangedAsync(roleId, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    return Result<SetRolePermissionsCommandResult>.Success(new SetRolePermissionsCommandResult(toAdd.Count, toRemove.Count));
  }
}
