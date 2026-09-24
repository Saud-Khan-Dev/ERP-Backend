public sealed record GetMySessionsQueryResult(IReadOnlyList<SessionDto> Sessions);

public sealed record GetMySessionsQuery(bool IncludeRevoked) : IQuery<Result<GetMySessionsQueryResult>>;
