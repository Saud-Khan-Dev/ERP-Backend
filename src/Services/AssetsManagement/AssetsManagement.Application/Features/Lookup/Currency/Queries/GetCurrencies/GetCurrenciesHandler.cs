using Microsoft.EntityFrameworkCore;

public class GetCurrenciesHandler(IApplicationDbContext context)
  : IQueryHandler<GetCurrenciesQuery, Result<GetCurrenciesQueryResult>>
{
  public async Task<Result<GetCurrenciesQueryResult>> Handle(GetCurrenciesQuery query, CancellationToken cancellationToken)
  {
    var currencies = context.Currencies.AsNoTracking();

    if (!query.IncludeInactive)
      currencies = currencies.Where(c => c.IsActive);

    var data = await currencies.OrderBy(c => c.Id).ToListAsync(cancellationToken);

    return Result<GetCurrenciesQueryResult>.Success(new GetCurrenciesQueryResult(data.Select(c => c.ToDto()).ToList()));
  }
}
