public sealed record GetCurrenciesQueryResult(IReadOnlyList<CurrencyLookupDto> Currencies);

public sealed record GetCurrenciesQuery(bool IncludeInactive) : IQuery<Result<GetCurrenciesQueryResult>>;
