public sealed record GetAssetsResponse(PaginatedResult<AssetListItemDto> Assets);

public class GetAssets : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapGet("/assets", async (
      ISender sender,
      HttpRequest http,
      int? pageIndex,
      int? pageSize,
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
      bool? sortDescending) =>
    {
      // dynamic attribute filters: ?attr=ram_gb:gte:16&attr=operating_system:eq:WINDOWS_11
      var filters = http.Query["attr"]
        .Where(v => !string.IsNullOrWhiteSpace(v))
        .Select(AssetQueryParsing.ParseFilter)
        .ToList();

      var query = new GetAssetsQuery(
        new PaginationRequest(pageIndex ?? 0, pageSize ?? 10),
        assetClassId, assetTypeId, categoryId, includeSubCategories ?? true,
        statusId, locationId, custodianId, departmentId,
        search, includeInactive ?? false, filters,
        includeSubLocations ?? true, AssetQueryParsing.ParseEnum<OwnershipType>(ownership, "ownership"), AssetQueryParsing.ParseEnum<DisposalFilter>(disposed, "disposed") ?? DisposalFilter.Include,
        registeredFrom, registeredTo, acquiredFrom, acquiredTo, costMin, costMax,
        AssetQueryParsing.ParseEnum<AssetSort>(sortBy, "sortBy") ?? AssetSort.Code, sortDescending ?? false);

      var result = await sender.Send(query);
      return Results.Ok(new GetAssetsResponse(result.Value!.Assets));
    })
      .WithName("GetAssets")
      .Produces<GetAssetsResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Get Assets")
      .WithDescription("Paginated asset register. Filters: taxonomy, status, location (with its sub-locations unless includeSubLocations=false), custodian, department, ownership, disposed (include|exclude|only), registeredFrom/To (date and time, UTC), acquiredFrom/To (purchase date), costMin/Max (purchase cost), free-text search (code, name, barcode, description) and dynamic attribute filters (attr=code:op:value, op = eq|neq|gt|gte|lt|lte|contains). sortBy = code|name|registered|acquired|cost, sortDescending.");
  }
}
