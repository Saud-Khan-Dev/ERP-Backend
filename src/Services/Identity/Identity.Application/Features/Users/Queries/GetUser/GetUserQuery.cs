public sealed record GetUserQueryResult(
  UserDto User,
  IReadOnlyList<UserRoleDto> Roles,
  IReadOnlyList<UserPermissionOverrideDto> Overrides,
  IReadOnlyList<string> EffectivePermissions,
  UserScopeDto Scope);

/// The full administrative view of one account: profile, role grants, overrides, and the permission
/// set those actually add up to.
/// `IncludeDeleted` opens a deleted account read-only (to show it, and to restore it).
public sealed record GetUserQuery(Guid Id, bool IncludeDeleted = false) : IQuery<Result<GetUserQueryResult>>;
