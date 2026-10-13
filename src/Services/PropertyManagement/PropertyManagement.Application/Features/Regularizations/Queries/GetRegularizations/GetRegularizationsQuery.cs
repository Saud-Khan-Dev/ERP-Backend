public sealed record GetRegularizationsQueryResult(IReadOnlyList<RegularizationDto> Regularizations);

/// Withdrawn cases are history, so they are listed by default (isActive = false); includeInactive=false
/// returns only the cases still in play.
public sealed record GetRegularizationsQuery(Guid PropertyId, bool IncludeInactive = true) : IQuery<Result<GetRegularizationsQueryResult>>;
