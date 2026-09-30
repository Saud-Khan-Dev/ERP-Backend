public sealed record GetPropertyEncumbrancesQueryResult(IReadOnlyList<EncumbranceDto> Encumbrances);

public sealed record GetPropertyEncumbrancesQuery(Guid PropertyId, EncumbranceStatus? Status = null) : IQuery<Result<GetPropertyEncumbrancesQueryResult>>;
