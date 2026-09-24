public sealed record GetRolesQueryResult(IReadOnlyList<RoleDto> Roles);

public sealed record GetRolesQuery(bool IncludeInactive) : IQuery<Result<GetRolesQueryResult>>;
