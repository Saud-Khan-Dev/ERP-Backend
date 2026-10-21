public sealed record GetAssetsResponse(PaginatedResult<AssetListItemDto> Assets);

public class GetAssets : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapGet("/assets", async (ISender sender, HttpRequest http, ICurrentUser user, int? pageIndex, int? pageSize) =>
    {
      var query = AssetQueryParsing.Read(http, user, new PaginationRequest(pageIndex ?? 0, pageSize ?? 10));
      var result = await sender.Send(query);
      return Results.Ok(new GetAssetsResponse(result.Value!.Assets));
    })
      .RequirePermission(PermissionCatalog.Assets.View)

      .WithName("GetAssets")
      .Produces<GetAssetsResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status403Forbidden)
      .WithSummary("Get Assets")
      .WithDescription(
        "Paginated asset register (deleted assets never listed). Filters: assetClassId / assetTypeId / categoryId (each on its own; " +
        "includeSubCategories), statusId, locationId (with the places inside it unless includeSubLocations=false) or noLocation=true, " +
        "custodianId (HRM employee id) or hasCustodian=true|false, departmentId (HRM unit id, with the units under it unless includeSubUnits=false), heldOn " +
        "(with custodianId: what the person held on that day), ownership, disposed (include|exclude|only), disposedFrom/To, " +
        "registeredFrom/To (UTC), loan (out|overdue), warrantyFrom/To (warranty end date) or warrantyWithinDays (ends between today and N days on), " +
        "outOfService (a status that neither ends the record nor allows issuing), statusSinceTo / inStatusOverDays (in its status since on or before), " +
        "missingRequiredLocation, search (part of the tag, name, barcode, description, or a Searchable field) and attr=code:op:value " +
        "(op = eq|neq|gt|gte|lt|lte|contains; only fields marked Filter). Money - the cost, purchase date and book value columns, and " +
        "the filters acquiredFrom/To, costMin/Max, hasPurchase, depreciationBehind and sortBy=acquired|cost - needs ASSET_FINANCE.VIEW: " +
        "without it the columns are empty and those filters are refused (403). sortBy = code|name|registered|acquired|cost|status|location|warranty, sortDescending.");
  }
}
