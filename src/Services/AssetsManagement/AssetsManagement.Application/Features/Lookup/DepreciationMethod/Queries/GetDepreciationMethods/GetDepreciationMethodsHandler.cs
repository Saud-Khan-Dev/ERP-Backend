using Microsoft.EntityFrameworkCore;

public class GetDepreciationMethodsHandler(IApplicationDbContext context)
  : IQueryHandler<GetDepreciationMethodsQuery, Result<GetDepreciationMethodsQueryResult>>
{
  public async Task<Result<GetDepreciationMethodsQueryResult>> Handle(GetDepreciationMethodsQuery query, CancellationToken cancellationToken)
  {
    var methods = context.DepreciationMethods.AsNoTracking();

    if (!query.IncludeInactive)
      methods = methods.Where(m => m.IsActive);

    var data = await methods.OrderBy(m => m.Code).ToListAsync(cancellationToken);

    return Result<GetDepreciationMethodsQueryResult>.Success(new GetDepreciationMethodsQueryResult(data.Select(m => m.ToDto()).ToList()));
  }
}
