/// One grantable operation, e.g. ASSETS.CREATE.
///
/// Permissions are seeded from the shared catalogue, not created ad hoc: a permission that no
/// endpoint checks is dead weight, and an endpoint checking a permission that was never seeded
/// would be unreachable.
public class Permission : Entity<PermissionId>
{
  public PermissionCode Code { get; private set; } = default!;
  public PermissionModuleId ModuleId { get; private set; } = default!;
  public PermissionAction Action { get; private set; }
  public Name PermissionName { get; private set; } = default!;
  public string? Description { get; private set; }
  public bool IsActive { get; private set; }

  public static Permission Create(
      PermissionId id,
      PermissionCode code,
      PermissionModuleId moduleId,
      PermissionAction action,
      Name name,
      string? description)
  {
    ArgumentNullException.ThrowIfNull(code);
    ArgumentNullException.ThrowIfNull(moduleId);
    ArgumentNullException.ThrowIfNull(name);

    if (!string.Equals(code.Action, action.ToString(), StringComparison.OrdinalIgnoreCase))
      throw new DomainException($"Permission code '{code.Value}' does not match action '{action}'.");

    return new Permission
    {
      Id = id,
      Code = code,
      ModuleId = moduleId,
      Action = action,
      PermissionName = name,
      Description = description,
      IsActive = true
    };
  }

  public void Update(Name name, string? description, bool isActive)
  {
    ArgumentNullException.ThrowIfNull(name);

    PermissionName = name;
    Description = description;
    IsActive = isActive;
  }
}
