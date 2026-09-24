/// The business area half of a permission code: ASSETS, INVENTORY, PROPERTY, IAM_USERS ...
/// A lookup table rather than free text, so module names cannot drift.
public class PermissionModule : Entity<PermissionModuleId>
{
  public LookupCode Code { get; private set; } = default!;
  public Name ModuleName { get; private set; } = default!;

  public static PermissionModule Create(PermissionModuleId id, LookupCode code, Name name)
  {
    ArgumentNullException.ThrowIfNull(code);
    ArgumentNullException.ThrowIfNull(name);

    return new PermissionModule { Id = id, Code = code, ModuleName = name };
  }

  public void Rename(Name name)
  {
    ArgumentNullException.ThrowIfNull(name);
    ModuleName = name;
  }
}
