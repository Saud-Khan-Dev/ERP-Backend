public sealed record GetAssetClassesResponse(IReadOnlyList<AssetClassDto> AssetClasses);

public class GetAssetClasses : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapGet("/asset-classes", async (ISender sender, bool? includeInactive) =>
    {
      var result = await sender.Send(new GetAssetClassesQuery(includeInactive ?? false));
      return Results.Ok(new GetAssetClassesResponse(result.Value!.AssetClasses));
    })
      .WithName("GetAssetClasses")
      .Produces<GetAssetClassesResponse>(StatusCodes.Status200OK)
      .WithSummary("Get Asset Classes")
      .WithDescription("Lists asset classes ordered by display order.");
  }
}
