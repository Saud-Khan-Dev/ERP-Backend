/// A per-user permission override either grants or withholds a permission.
/// DENY always beats any role grant — see PermissionResolver.
public enum OverrideEffect
{
  Allow,
  Deny
}
