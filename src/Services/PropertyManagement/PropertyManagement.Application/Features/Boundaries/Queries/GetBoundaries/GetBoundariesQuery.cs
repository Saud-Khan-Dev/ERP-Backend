public sealed record GetBoundariesQueryResult(IReadOnlyList<BoundaryDto> Boundaries);
public sealed record GetBoundaryQueryResult(BoundaryDto Boundary);

/// Every survey (current first); currentOnly=true returns just the boundary in force.
public sealed record GetPropertyBoundariesQuery(Guid PropertyId, bool CurrentOnly = false) : IQuery<Result<GetBoundariesQueryResult>>;

public sealed record GetBoundaryQuery(Guid Id) : IQuery<Result<GetBoundaryQueryResult>>;
