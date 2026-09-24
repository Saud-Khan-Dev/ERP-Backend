public sealed record GetUserQueryResult(
  UserDto User,
  IReadOnlyList<UserRoleDto> Roles,
  IReadOnlyList<UserPermissionOverrideDto> Overrides,
  IReadOnlyList<string> EffectivePermissions);

/// The full administrative view of one account: profile, role grants, overrides, and the permission
/// set those actually add up to.
public sealed record GetUserQuery(Guid Id) : IQuery<Result<GetUserQueryResult>>;
