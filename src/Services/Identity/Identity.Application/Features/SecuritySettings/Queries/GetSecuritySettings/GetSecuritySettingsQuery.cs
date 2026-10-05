public sealed record GetSecuritySettingsQueryResult(SecuritySettingsDto Settings);

public sealed record GetSecuritySettingsQuery : IQuery<Result<GetSecuritySettingsQueryResult>>;
