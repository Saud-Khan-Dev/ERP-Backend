public sealed record GetAppealsQueryResult(IReadOnlyList<AppealDto> Appeals);
public sealed record GetAppealQueryResult(AppealDto Appeal);

public sealed record GetPropertyAppealsQuery(Guid PropertyId) : IQuery<Result<GetAppealsQueryResult>>;

public sealed record GetAppealQuery(Guid Id) : IQuery<Result<GetAppealQueryResult>>;

/// Open appeals past their 120-day decision date (Act s.32(1)), across all properties.
public sealed record GetOverdueAppealsQuery : IQuery<Result<GetAppealsQueryResult>>;
