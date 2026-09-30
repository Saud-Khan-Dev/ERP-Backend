public sealed record GetStatusHistoryQueryResult(IReadOnlyList<StatusHistoryDto> History);

public sealed record GetStatusHistoryQuery(Guid PropertyId) : IQuery<Result<GetStatusHistoryQueryResult>>;
