public sealed record GetOutsourcingsQueryResult(IReadOnlyList<OutsourcingDto> Contracts);
public sealed record GetOutsourcingQueryResult(OutsourcingDto Contract);

public sealed record GetPropertyOutsourcingsQuery(Guid PropertyId) : IQuery<Result<GetOutsourcingsQueryResult>>;

public sealed record GetOutsourcingQuery(Guid Id) : IQuery<Result<GetOutsourcingQueryResult>>;
