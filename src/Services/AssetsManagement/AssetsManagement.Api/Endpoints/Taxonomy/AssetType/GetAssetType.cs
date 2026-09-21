public sealed record GetAssetTypeResponse(AssetTypeDto AssetType);

public class GetAssetType : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapGet("/asset-types/{id}", async (Guid id, ISender sender) =>
    {
      var result = await sender.Send(new GetAssetTypeQuery(id));
      return Results.Ok(new GetAssetTypeResponse(result.Value!.AssetType));
    })
      .WithName("GetAssetType")
      .Produces<GetAssetTypeResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Get Asset Type")
      .WithDescription("Get Asset Type");
  }
}
