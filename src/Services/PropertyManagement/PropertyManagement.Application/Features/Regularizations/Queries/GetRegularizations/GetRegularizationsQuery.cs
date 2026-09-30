public sealed record GetRegularizationsQueryResult(IReadOnlyList<RegularizationDto> Regularizations);

public sealed record GetRegularizationsQuery(Guid PropertyId, bool IncludeInactive = false) : IQuery<Result<GetRegularizationsQueryResult>>;
