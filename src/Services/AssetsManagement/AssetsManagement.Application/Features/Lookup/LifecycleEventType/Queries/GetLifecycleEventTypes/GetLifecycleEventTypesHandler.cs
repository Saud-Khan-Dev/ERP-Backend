using Microsoft.EntityFrameworkCore;

public class GetLifecycleEventTypesHandler(IApplicationDbContext context)
  : IQueryHandler<GetLifecycleEventTypesQuery, Result<GetLifecycleEventTypesQueryResult>>
{
  public async Task<Result<GetLifecycleEventTypesQueryResult>> Handle(GetLifecycleEventTypesQuery query, CancellationToken cancellationToken)
  {
    var types = context.LifecycleEventTypes.AsNoTracking();

    if (!string.IsNullOrWhiteSpace(query.Stage))
    {
      var stage = query.Stage.Trim().ToUpperInvariant();
      types = types.Where(t => t.Stage == stage);
    }

    if (!query.IncludeInactive)
      types = types.Where(t => t.IsActive);

    var data = await types.OrderBy(t => t.Stage).ThenBy(t => t.Code).ToListAsync(cancellationToken);

    return Result<GetLifecycleEventTypesQueryResult>.Success(new GetLifecycleEventTypesQueryResult(data.Select(t => t.ToDto()).ToList()));
  }
}
