using Microsoft.EntityFrameworkCore;

public class AddRolePermissionsHandler(IApplicationDbContext context, IdentityGuard guard, ICurrentUser currentUser)
  : ICommandHandler<AddRolePermissionsCommand, Result<AddRolePermissionsCommandResult>>
{
  public async Task<Result<AddRolePermissionsCommandResult>> Handle(AddRolePermissionsCommand command, CancellationToken cancellationToken)
  {
    var now = DateTime.UtcNow;
    var roleId = RoleId.Of(command.RoleId);

    var role = await context.Roles.AsNoTracking().FirstOrDefaultAsync(r => r.Id == roleId, cancellationToken)
      ?? throw new RoleNotFoundException($"Role {command.RoleId} was not found.");

    // only a Super Admin may change what SUPER_ADMIN can do
    guard.EnsureCanAdministerRole(role);

    var ids = command.PermissionIds.Distinct().Select(PermissionId.Of).ToList();
    var found = await context.Permissions.CountAsync(p => ids.Contains(p.Id), cancellationToken);

    if (found != ids.Count)
      throw new PermissionNotFoundException("One or more of the supplied permissions was not found.");

    var existing = await context.RolePermissions
        .Where(rp => rp.RoleId == roleId && ids.Contains(rp.PermissionId))
        .Select(rp => rp.PermissionId)
        .ToListAsync(cancellationToken);

    var toAdd = ids.Except(existing).ToList();

    foreach (var permissionId in toAdd)
      await context.RolePermissions.AddAsync(
        RolePermission.Create(roleId, permissionId, currentUser.UserId, now), cancellationToken);

    await context.SaveChangesAsync(cancellationToken);

    return Result<AddRolePermissionsCommandResult>.Success(new AddRolePermissionsCommandResult(toAdd.Count));
  }
}
