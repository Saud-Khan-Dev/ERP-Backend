/// A named bundle of permissions. Users are given roles; roles carry the permissions.
public class Role : Aggregate<RoleId>
{
  /// The built-in role that may administer everything. Protected from rename, deactivation and deletion.
  public const string SuperAdminCode = "SUPER_ADMIN";

  public LookupCode Code { get; private set; } = default!;
  public Name RoleName { get; private set; } = default!;
  public string? Description { get; private set; }

  /// Shipped with the product. System roles cannot be renamed, deactivated or deleted through the API.
  public bool IsSystem { get; private set; }

  public bool IsActive { get; private set; }
  public DateTime? DeletedAt { get; private set; }

  public bool IsDeleted => DeletedAt.HasValue;

  public bool IsSuperAdmin => Code.Value == SuperAdminCode;

  public static Role Create(RoleId id, LookupCode code, Name name, string? description, bool isSystem)
  {
    ArgumentNullException.ThrowIfNull(code);
    ArgumentNullException.ThrowIfNull(name);

    return new Role
    {
      Id = id,
      Code = code,
      RoleName = name,
      Description = description,
      IsSystem = isSystem,
      IsActive = true
    };
  }

  public void Update(LookupCode code, Name name, string? description, bool isActive)
  {
    ArgumentNullException.ThrowIfNull(code);
    ArgumentNullException.ThrowIfNull(name);
    EnsureNotDeleted();

    if (IsSystem && code != Code)
      throw new DomainException($"System role '{Code.Value}' cannot be renamed.");

    if (IsSystem && !isActive)
      throw new DomainException($"System role '{Code.Value}' cannot be deactivated.");

    Code = code;
    RoleName = name;
    Description = description;
    IsActive = isActive;
  }

  public void EnsureDeletable()
  {
    if (IsSystem)
      throw new DomainException($"System role '{Code.Value}' cannot be deleted.");
  }

  public void SoftDelete(DateTime now)
  {
    EnsureDeletable();
    EnsureNotDeleted();

    DeletedAt = now;
    IsActive = false;
  }

  private void EnsureNotDeleted()
  {
    if (IsDeleted)
      throw new DomainException("This role has been deleted.");
  }
}
