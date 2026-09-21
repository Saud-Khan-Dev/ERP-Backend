using Microsoft.EntityFrameworkCore;

public class GetDepreciationSchedulesHandler(IApplicationDbContext context)
  : IQueryHandler<GetDepreciationSchedulesQuery, Result<GetDepreciationSchedulesQueryResult>>
{
  public async Task<Result<GetDepreciationSchedulesQueryResult>> Handle(GetDepreciationSchedulesQuery query, CancellationToken cancellationToken)
  {
    var assetId = AssetId.Of(query.AssetId);
    if (!await context.Assets.IgnoreQueryFilters().AnyAsync(a => a.Id == assetId, cancellationToken))
      throw new AssetNotFoundException($"Asset {query.AssetId} was not found.");

    var schedules = context.AssetDepreciationSchedules.AsNoTracking().Include(s => s.Entries).Where(s => s.AssetId == assetId);

    if (!query.IncludeInactive)
      schedules = schedules.Where(s => s.IsActive);

    var data = await schedules.OrderByDescending(s => s.StartDate).ToListAsync(cancellationToken);

    var acquisition = await context.AssetAcquisitions.AsNoTracking().FirstOrDefaultAsync(a => a.AssetId == assetId, cancellationToken);
    var active = data.FirstOrDefault(s => s.IsActive);
    decimal? netBookValue = acquisition is null
      ? null
      : active?.NetBookValue(acquisition.AcquisitionCost) ?? acquisition.AcquisitionCost;

    return Result<GetDepreciationSchedulesQueryResult>.Success(
      new GetDepreciationSchedulesQueryResult(data.Select(s => s.ToDto()).ToList(), netBookValue));
  }
}
