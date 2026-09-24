public sealed record GetRoleQueryResult(RoleDto Role, IReadOnlyList<PermissionDto> Permissions);

public sealed record GetRoleQuery(Guid Id) : IQuery<Result<GetRoleQueryResult>>;
