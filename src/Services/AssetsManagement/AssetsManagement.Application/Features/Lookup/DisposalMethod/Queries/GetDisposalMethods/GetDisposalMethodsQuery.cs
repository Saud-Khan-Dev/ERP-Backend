public sealed record GetDisposalMethodsQueryResult(IReadOnlyList<DisposalMethodDto> Methods);

public sealed record GetDisposalMethodsQuery(bool IncludeInactive) : IQuery<Result<GetDisposalMethodsQueryResult>>;
