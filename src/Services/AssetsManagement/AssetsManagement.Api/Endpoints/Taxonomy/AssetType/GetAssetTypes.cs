public sealed record GetAssetTypesResponse(IReadOnlyList<AssetTypeDto> AssetTypes);

public class GetAssetTypes : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapGet("/asset-types", async (ISender sender, Guid? assetClassId, bool? includeInactive) =>
    {
      var result = await sender.Send(new GetAssetTypesQuery(assetClassId, includeInactive ?? false));
      return Results.Ok(new GetAssetTypesResponse(result.Value!.AssetTypes));
    })
      .WithName("GetAssetTypes")
      .Produces<GetAssetTypesResponse>(StatusCodes.Status200OK)
      .WithSummary("Get Asset Types")
      .WithDescription("Lists asset types, optionally for one asset class.");
  }
}
