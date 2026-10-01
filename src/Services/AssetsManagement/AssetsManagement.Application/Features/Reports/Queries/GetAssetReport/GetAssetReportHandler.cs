using Microsoft.EntityFrameworkCore;

public class GetAssetReportHandler(IApplicationDbContext context)
  : IQueryHandler<GetAssetReportQuery, Result<GetAssetReportQueryResult>>
{
  /// A report is a document someone reads or prints; beyond this it is an export job, not a report.
  public const int MaxRows = 5000;

  public async Task<Result<GetAssetReportQueryResult>> Handle(GetAssetReportQuery query, CancellationToken cancellationToken)
  {
    var asOf = query.AsOf ?? DateOnly.FromDateTime(DateTime.UtcNow);

    IQueryable<Asset> assets;
    if (query.Ids is { Count: > 0 })
    {
      var ids = query.Ids.Distinct().Select(AssetId.Of).ToList();
      assets = context.Assets.AsNoTracking().Where(a => ids.Contains(a.Id));
    }
    else
    {
      assets = await AssetQueryFilter.ApplyAsync(context, query.Filter, cancellationToken);
    }

    var count = await assets.CountAsync(cancellationToken);
    if (count > MaxRows && !query.TotalsOnly)
      return Result<GetAssetReportQueryResult>.Failure($"The report would list {count} assets. Narrow the filters to {MaxRows} or fewer.");

    var page = await AssetQueryFilter.Order(context, assets, query.Filter).ToListAsync(cancellationToken);
    var assetIds = page.Select(a => a.Id).ToList();

    var acquisitions = await context.AssetAcquisitions.AsNoTracking()
      .Where(q => assetIds.Contains(q.AssetId)).ToDictionaryAsync(q => q.AssetId, cancellationToken);
    var disposals = await context.AssetDisposals.AsNoTracking()
      .Where(d => assetIds.Contains(d.AssetId)).ToDictionaryAsync(d => d.AssetId, cancellationToken);
    var schedules = (await context.AssetDepreciationSchedules.AsNoTracking().Include(s => s.Entries)
        .Where(s => assetIds.Contains(s.AssetId)).ToListAsync(cancellationToken))
      .GroupBy(s => s.AssetId).ToDictionary(g => g.Key, g => g.ToList());
    var valuations = (await context.AssetValuations.AsNoTracking()
        .Where(v => assetIds.Contains(v.AssetId) && v.ValuationDate <= asOf).ToListAsync(cancellationToken))
      .GroupBy(v => v.AssetId).ToDictionary(g => g.Key, g => g.OrderByDescending(v => v.ValuationDate).ToList());

    var rows = new List<AssetReportRowDto>(page.Count);
    foreach (var asset in page)
    {
      var acquisition = acquisitions.GetValueOrDefault(asset.Id);
      var disposal = disposals.GetValueOrDefault(asset.Id);
      var assetSchedules = schedules.GetValueOrDefault(asset.Id) ?? [];
      var plan = assetSchedules.FirstOrDefault(s => s.IsActive) ?? assetSchedules.OrderByDescending(s => s.StartDate).FirstOrDefault();
      var assetValuations = valuations.GetValueOrDefault(asset.Id) ?? [];
      var latest = assetValuations.FirstOrDefault();

      // Written off = every posted, not reversed month that ended on or before the report date, whichever schedule wrote it.
      var accumulated = assetSchedules.SelectMany(s => s.Entries)
        .Where(e => e.Posted && !e.IsReversed && e.PeriodEnd <= asOf)
        .Sum(e => e.DepreciationAmount);

      rows.Add(new AssetReportRowDto(
        asset.Id.Value, asset.AssetCode.Value, asset.Name.Value, asset.Description, asset.Ownership,
        asset.AssetClassId.Value, asset.AssetTypeId.Value, asset.CategoryId.Value, asset.StatusId.Value,
        asset.DepartmentId, asset.CustodianId, asset.CurrentLocationId?.Value, asset.Barcode, asset.IsActive, asset.CreatedAt,
        asset.ExtraAttributes,
        acquisition?.AcquisitionDate, acquisition?.AcquisitionType, acquisition?.AcquisitionCost, acquisition?.CurrencyCode.Value,
        acquisition?.PurchaseReference, acquisition?.SupplierId, acquisition?.WarrantyExpiryDate,
        plan?.MethodId.Value, plan?.UsefulLifeMonths, plan?.StartDate,
        accumulated, acquisition is null ? null : acquisition.AcquisitionCost - accumulated,
        latest?.Value, latest?.ValuationDate, latest?.CurrencyCode.Value, latest?.ValuationMethod, assetValuations.Count,
        disposal?.DisposalDate, disposal?.DisposalMethodId.Value, disposal?.DisposalValue, disposal?.NetBookValueAtDisposal, disposal?.GainLoss));
    }

    var currencies = rows.Select(r => r.CurrencyCode).Concat(rows.Select(r => r.LatestValuationCurrency))
      .Where(c => c is not null).Select(c => c!).Distinct().OrderBy(c => c, StringComparer.Ordinal);
    var totals = currencies.Select(code =>
    {
      var bought = rows.Where(r => r.CurrencyCode == code).ToList();
      var valued = rows.Where(r => r.LatestValuationCurrency == code).ToList();
      var sold = rows.Where(r => r.DisposalDate.HasValue && r.CurrencyCode == code).ToList();
      return new AssetReportTotalDto(
        code, bought.Count, bought.Sum(r => r.AcquisitionCost ?? 0), bought.Sum(r => r.AccumulatedDepreciation), bought.Sum(r => r.BookValue ?? 0),
        valued.Count, valued.Sum(r => r.LatestValuation ?? 0), sold.Count, sold.Sum(r => r.DisposalValue ?? 0));
    }).ToList();

    return Result<GetAssetReportQueryResult>.Success(new GetAssetReportQueryResult(asOf, rows.Count, query.TotalsOnly ? [] : rows, totals));
  }
}
