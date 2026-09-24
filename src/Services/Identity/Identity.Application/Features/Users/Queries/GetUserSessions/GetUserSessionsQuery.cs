public sealed record GetUserSessionsQueryResult(IReadOnlyList<SessionDto> Sessions);

public sealed record GetUserSessionsQuery(Guid UserId, bool IncludeRevoked) : IQuery<Result<GetUserSessionsQueryResult>>;
