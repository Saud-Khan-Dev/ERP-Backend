public sealed record GetViolationsQueryResult(IReadOnlyList<ViolationDto> Violations);
public sealed record GetViolationQueryResult(ViolationDto Violation);

public sealed record GetPropertyViolationsQuery(Guid PropertyId, Guid? LeaseId = null, Guid? RentalId = null, Guid? TransferId = null) : IQuery<Result<GetViolationsQueryResult>>;

public sealed record GetViolationQuery(Guid Id) : IQuery<Result<GetViolationQueryResult>>;
