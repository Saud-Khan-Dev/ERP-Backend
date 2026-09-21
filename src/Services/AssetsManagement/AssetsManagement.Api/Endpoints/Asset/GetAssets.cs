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
      Guid? parentAssetId,
      string? search,
      bool? includeInactive) =>
    {
      // dynamic attribute filters: ?attr=ram_gb:gte:16&attr=operating_system:eq:WINDOWS_11
      var filters = http.Query["attr"]
        .Where(v => !string.IsNullOrWhiteSpace(v))
        .Select(ParseFilter)
        .ToList();

      var query = new GetAssetsQuery(
        new PaginationRequest(pageIndex ?? 0, pageSize ?? 10),
        assetClassId, assetTypeId, categoryId, includeSubCategories ?? true,
        statusId, locationId, custodianId, departmentId, parentAssetId,
        search, includeInactive ?? false, filters);

      var result = await sender.Send(query);
      return Results.Ok(new GetAssetsResponse(result.Value!.Assets));
    })
      .WithName("GetAssets")
      .Produces<GetAssetsResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Get Assets")
      .WithDescription("Paginated asset list with taxonomy / status / location / custodian filters, free-text search and dynamic attribute filters (attr=code:op:value, op = eq|neq|gt|gte|lt|lte|contains).");
  }

  private static AttributeFilter ParseFilter(string? raw)
  {
    var parts = raw!.Split(':', 3);
    if (parts.Length != 3 || !Enum.TryParse<AttributeFilterOperator>(parts[1], ignoreCase: true, out var op))
      throw new BadHttpRequestException($"Invalid attribute filter '{raw}'. Expected code:op:value.");

    return new AttributeFilter(parts[0], op, parts[2]);
  }
}
