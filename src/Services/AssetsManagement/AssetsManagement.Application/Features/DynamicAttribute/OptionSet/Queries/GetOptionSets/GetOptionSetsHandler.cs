using Microsoft.EntityFrameworkCore;

public class GetOptionSetsHandler(IApplicationDbContext context)
  : IQueryHandler<GetOptionSetsQuery, Result<GetOptionSetsQueryResult>>
{
  public async Task<Result<GetOptionSetsQueryResult>> Handle(GetOptionSetsQuery query, CancellationToken cancellationToken)
  {
    var sets = context.OptionSets.AsNoTracking().Include(o => o.Values).AsQueryable();

    if (!query.IncludeInactive)
      sets = sets.Where(o => o.IsActive);

    var data = await sets.OrderBy(o => o.Code).ToListAsync(cancellationToken);

    return Result<GetOptionSetsQueryResult>.Success(new GetOptionSetsQueryResult(data.Select(o => o.ToDto()).ToList()));
  }
}
