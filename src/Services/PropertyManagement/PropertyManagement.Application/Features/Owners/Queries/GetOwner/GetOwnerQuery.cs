public sealed record GetOwnerQueryResult(OwnerDto Owner);

public sealed record GetOwnerQuery(Guid Id) : IQuery<Result<GetOwnerQueryResult>>;
