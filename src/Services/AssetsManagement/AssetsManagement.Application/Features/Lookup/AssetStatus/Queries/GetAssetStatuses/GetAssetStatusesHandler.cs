using Microsoft.EntityFrameworkCore;

public class GetAssetStatusesHandler(IApplicationDbContext context)
  : IQueryHandler<GetAssetStatusesQuery, Result<GetAssetStatusesQueryResult>>
{
  public async Task<Result<GetAssetStatusesQueryResult>> Handle(GetAssetStatusesQuery query, CancellationToken cancellationToken)
  {
    var statuses = context.AssetStatuses.AsNoTracking();

    if (!query.IncludeInactive)
      statuses = statuses.Where(s => s.IsActive);

    var data = await statuses.OrderBy(s => s.DisplayOrder).ThenBy(s => s.Code).ToListAsync(cancellationToken);

    return Result<GetAssetStatusesQueryResult>.Success(new GetAssetStatusesQueryResult(data.Select(s => s.ToDto()).ToList()));
  }
}
