public sealed record GetOptionSetsQueryResult(IReadOnlyList<OptionSetDto> OptionSets);

public sealed record GetOptionSetsQuery(bool IncludeInactive) : IQuery<Result<GetOptionSetsQueryResult>>;
