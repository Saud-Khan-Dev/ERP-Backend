public sealed record GetMastersQueryResult(string Type, IReadOnlyList<MasterDto> Items);

public sealed record GetMastersQuery(string Type, bool IncludeInactive = false) : IQuery<Result<GetMastersQueryResult>>;
