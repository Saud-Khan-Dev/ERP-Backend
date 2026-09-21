public sealed record GetDepreciationMethodsQueryResult(IReadOnlyList<DepreciationMethodDto> Methods);

public sealed record GetDepreciationMethodsQuery(bool IncludeInactive) : IQuery<Result<GetDepreciationMethodsQueryResult>>;
