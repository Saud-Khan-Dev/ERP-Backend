public sealed record GetPropertyQueryResult(PropertyDto Property);

public sealed record GetPropertyQuery(Guid Id) : IQuery<Result<GetPropertyQueryResult>>;
