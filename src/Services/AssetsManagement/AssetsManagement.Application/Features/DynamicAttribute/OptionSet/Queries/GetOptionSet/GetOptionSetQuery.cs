public sealed record GetOptionSetQueryResult(OptionSetDto OptionSet);

public sealed record GetOptionSetQuery(Guid Id) : IQuery<Result<GetOptionSetQueryResult>>;
