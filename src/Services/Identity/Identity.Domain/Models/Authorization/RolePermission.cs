/// Attaches a permission to a role. Composite key (role_id, permission_id): detaching removes the row.
public class RolePermission : Entity<Guid>
{
  public RoleId RoleId { get; private set; } = default!;
  public PermissionId PermissionId { get; private set; } = default!;
  public DateTime AssignedAt { get; private set; }
  public Guid? AssignedBy { get; private set; }

  public static RolePermission Create(RoleId roleId, PermissionId permissionId, Guid? assignedBy, DateTime now)
  {
    ArgumentNullException.ThrowIfNull(roleId);
    ArgumentNullException.ThrowIfNull(permissionId);

    return new RolePermission
    {
      RoleId = roleId,
      PermissionId = permissionId,
      AssignedAt = now,
      AssignedBy = assignedBy
    };
  }
}
