public sealed record GetPermissionsQueryResult(IReadOnlyList<PermissionDto> Permissions);

/// The catalogue an administrator picks from when building a role.
public sealed record GetPermissionsQuery(string? Module = null, bool IncludeInactive = false)
  : IQuery<Result<GetPermissionsQueryResult>>;
