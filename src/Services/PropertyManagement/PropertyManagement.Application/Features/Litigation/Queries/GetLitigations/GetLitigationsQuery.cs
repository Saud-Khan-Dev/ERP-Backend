public sealed record GetLitigationsQueryResult(IReadOnlyList<LitigationDto> Cases);
public sealed record GetLitigationQueryResult(LitigationDto Case);

public sealed record GetPropertyLitigationsQuery(Guid PropertyId) : IQuery<Result<GetLitigationsQueryResult>>;

public sealed record GetLitigationQuery(Guid Id) : IQuery<Result<GetLitigationQueryResult>>;

/// The hearing diary: every open case with a hearing between today and the next `Days` days.
public sealed record GetUpcomingHearingsQuery(int Days = 30) : IQuery<Result<GetLitigationsQueryResult>>;
