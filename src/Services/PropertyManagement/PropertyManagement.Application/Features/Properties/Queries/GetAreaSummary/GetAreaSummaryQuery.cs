public sealed record GetAreaSummaryQueryResult(AreaSummaryDto Area);

public sealed record GetAreaSummaryQuery(Guid PropertyId) : IQuery<Result<GetAreaSummaryQueryResult>>;
