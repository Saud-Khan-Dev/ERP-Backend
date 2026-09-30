public sealed record GetLeasesQueryResult(IReadOnlyList<LeaseDto> Leases);
public sealed record GetLeaseQueryResult(LeaseDto Lease);

public sealed record GetPropertyLeasesQuery(Guid PropertyId) : IQuery<Result<GetLeasesQueryResult>>;

public sealed record GetLeaseQuery(Guid Id) : IQuery<Result<GetLeaseQueryResult>>;
