public sealed record GetAssetReportResponse(DateOnly AsOf, int Count, IReadOnlyList<AssetReportRowDto> Rows, IReadOnlyList<AssetReportTotalDto> Totals);

/// Data for the Assets reports (the PDF / CSV exports are produced by the client from it).
public class AssetReportEndpoints : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapGet("/asset-reports/register", async (
      ISender sender,
      HttpRequest http,
      Guid? assetClassId,
      Guid? assetTypeId,
      Guid? categoryId,
      bool? includeSubCategories,
      Guid? statusId,
      Guid? locationId,
      Guid? custodianId,
      Guid? departmentId,
      string? search,
      bool? includeInactive,
      bool? includeSubLocations,
      string? ownership,
      string? disposed,
      DateTime? registeredFrom,
      DateTime? registeredTo,
      DateOnly? acquiredFrom,
      DateOnly? acquiredTo,
      decimal? costMin,
      decimal? costMax,
      string? sortBy,
      bool? sortDescending,
      DateOnly? asOf,
      bool? totalsOnly) =>
    {
      // explicit selection: ?ids=…&ids=… (then the filters are ignored)
      var ids = new List<Guid>();
      foreach (var raw in http.Query["ids"])
      {
        if (!Guid.TryParse(raw, out var id)) throw new BadHttpRequestException($"'{raw}' is not an asset id.");
        ids.Add(id);
      }

      var filter = new GetAssetsQuery(
        new PaginationRequest(0, 1),
        assetClassId, assetTypeId, categoryId, includeSubCategories ?? true,
        statusId, locationId, custodianId, departmentId,
        search, includeInactive ?? false, AssetQueryParsing.AttributeFilters(http),
        includeSubLocations ?? true, AssetQueryParsing.ParseEnum<OwnershipType>(ownership, "ownership"),
        AssetQueryParsing.ParseEnum<DisposalFilter>(disposed, "disposed") ?? DisposalFilter.Include,
        registeredFrom, registeredTo, acquiredFrom, acquiredTo, costMin, costMax,
        AssetQueryParsing.ParseEnum<AssetSort>(sortBy, "sortBy") ?? AssetSort.Code, sortDescending ?? false);

      var result = await sender.Send(new GetAssetReportQuery(filter, ids, asOf, totalsOnly ?? false));
      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });

      var report = result.Value!;
      return Results.Ok(new GetAssetReportResponse(report.AsOf, report.Count, report.Rows, report.Totals));
    })
      .WithName("GetAssetReport")
      .Produces<GetAssetReportResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Get Asset Report")
      .WithDescription("Every asset matching the register's filters (same parameters as GET /assets, no paging, at most 5000), or the assets given as ids=…, with purchase, depreciation written off and book value up to asOf (default today), latest valuation on or before asOf, disposal, and the full extra fields. Totals are per currency; totalsOnly=true returns the totals without the rows (no row limit).");
  }
}
