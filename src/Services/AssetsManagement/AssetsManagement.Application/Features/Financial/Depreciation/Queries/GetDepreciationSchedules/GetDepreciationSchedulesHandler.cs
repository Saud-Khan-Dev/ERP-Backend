using Microsoft.EntityFrameworkCore;

public class GetDepreciationSchedulesHandler(IApplicationDbContext context, IClock clock)
  : IQueryHandler<GetDepreciationSchedulesQuery, Result<GetDepreciationSchedulesQueryResult>>
{
  public async Task<Result<GetDepreciationSchedulesQueryResult>> Handle(GetDepreciationSchedulesQuery query, CancellationToken cancellationToken)
  {
    var assetId = AssetId.Of(query.AssetId);
    if (!await context.Assets.AnyAsync(a => a.Id == assetId, cancellationToken))
      throw new AssetNotFoundException("This asset was not found. It may have been deleted.");

    var all = await context.AssetDepreciationSchedules.AsNoTracking().Include(s => s.Entries)
      .Where(s => s.AssetId == assetId)
      .OrderByDescending(s => s.StartDate)
      .ToListAsync(cancellationToken);

    var acquisition = await context.AssetAcquisitions.AsNoTracking().FirstOrDefaultAsync(a => a.AssetId == assetId, cancellationToken);
    var writtenOff = AssetBookValue.WrittenOff(all, clock.Today);
    decimal? netBookValue = acquisition is null ? null : acquisition.AcquisitionCost - writtenOff;
    var drafts = all.SelectMany(s => s.Entries).Count(e => !e.Posted && !e.IsReversed);

    var shown = query.IncludeInactive ? all : all.Where(s => s.IsActive).ToList();
    return Result<GetDepreciationSchedulesQueryResult>.Success(new GetDepreciationSchedulesQueryResult(
      shown.Select(s => s.ToDto()).ToList(), netBookValue, writtenOff, AssetBookValue.PostedThrough(all), drafts));
  }
}
