public sealed record GetPermissionModulesQueryResult(IReadOnlyList<PermissionModuleDto> Modules);

public sealed record GetPermissionModulesQuery() : IQuery<Result<GetPermissionModulesQueryResult>>;
