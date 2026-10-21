using Microsoft.EntityFrameworkCore;

public class GetBookValueHandler(IApplicationDbContext context, IClock clock)
  : IQueryHandler<GetBookValueQuery, Result<GetBookValueQueryResult>>
{
  public async Task<Result<GetBookValueQueryResult>> Handle(GetBookValueQuery query, CancellationToken cancellationToken)
  {
    var assetId = AssetId.Of(query.AssetId);
    if (!await context.Assets.AnyAsync(a => a.Id == assetId, cancellationToken))
      throw new AssetNotFoundException("This asset was not found. It may have been deleted.");

    var asOf = query.AsOf ?? clock.Today;
    var acquisition = await context.AssetAcquisitions.AsNoTracking().FirstOrDefaultAsync(a => a.AssetId == assetId, cancellationToken);
    var schedules = await context.AssetDepreciationSchedules.AsNoTracking().Include(s => s.Entries).Where(s => s.AssetId == assetId).ToListAsync(cancellationToken);
    var writtenOff = AssetBookValue.WrittenOff(schedules, asOf);

    // what a disposal on that date would still post: draft months ended by then, plus months not written yet
    var toPost = 0;
    var amount = 0m;
    var limit = asOf < clock.Today ? asOf : clock.Today;
    foreach (var schedule in schedules.Where(s => s.IsActive))
    {
      var method = await context.DepreciationMethods.AsNoTracking().FirstAsync(m => m.Id == schedule.MethodId, cancellationToken);
      var drafts = schedule.Entries.Where(e => !e.Posted && !e.IsReversed && e.LastDay <= limit).ToList();
      toPost += drafts.Count;
      amount += drafts.Sum(e => e.DepreciationAmount);
      if (method.Kind != DepreciationMethodKind.UnitsOfProduction && acquisition is not null)
      {
        // work the missing months out on the side, nothing is saved
        var preview = schedule.GenerateEntries(method, asOf).Where(e => e.LastDay <= limit).ToList();
        toPost += preview.Count;
        amount += preview.Sum(e => e.DepreciationAmount);
      }
    }

    decimal? bookValue = acquisition is null ? null : acquisition.AcquisitionCost - writtenOff - amount;
    return Result<GetBookValueQueryResult>.Success(new GetBookValueQueryResult(
      asOf, acquisition?.AcquisitionCost, acquisition?.CurrencyCode.Value, writtenOff + amount, bookValue,
      AssetBookValue.PostedThrough(schedules), toPost, amount));
  }
}
