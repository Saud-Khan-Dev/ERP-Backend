using Microsoft.EntityFrameworkCore;

public class GetDisposalMethodsHandler(IApplicationDbContext context)
  : IQueryHandler<GetDisposalMethodsQuery, Result<GetDisposalMethodsQueryResult>>
{
  public async Task<Result<GetDisposalMethodsQueryResult>> Handle(GetDisposalMethodsQuery query, CancellationToken cancellationToken)
  {
    var methods = context.DisposalMethods.AsNoTracking();

    if (!query.IncludeInactive)
      methods = methods.Where(m => m.IsActive);

    var data = await methods.OrderBy(m => m.Code).ToListAsync(cancellationToken);

    return Result<GetDisposalMethodsQueryResult>.Success(new GetDisposalMethodsQueryResult(data.Select(m => m.ToDto()).ToList()));
  }
}
