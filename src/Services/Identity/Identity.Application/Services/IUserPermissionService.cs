/// Loads a user's role grants and overrides and folds them into the effective permission set
/// (see <see cref="PermissionResolver"/>). Used when issuing a token and by GET /auth/me.
public interface IUserPermissionService
{
  Task<PermissionResolver.ResolvedPermissions> ResolveAsync(UserId userId, CancellationToken cancellationToken);

  /// Role code -> role name, for the roles a token or /auth/me lists (a screen shows names, never codes).
  Task<IReadOnlyDictionary<string, string>> RoleNamesAsync(IEnumerable<string> roleCodes, CancellationToken cancellationToken);
}
