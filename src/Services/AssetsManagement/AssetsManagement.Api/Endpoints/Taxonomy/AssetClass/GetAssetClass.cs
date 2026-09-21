public sealed record GetAssetClassResponse(AssetClassDto AssetClass);

public class GetAssetClass : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapGet("/asset-classes/{id}", async (Guid id, ISender sender) =>
    {
      var result = await sender.Send(new GetAssetClassQuery(id));
      return Results.Ok(new GetAssetClassResponse(result.Value!.AssetClass));
    })
      .WithName("GetAssetClass")
      .Produces<GetAssetClassResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Get Asset Class")
      .WithDescription("Get Asset Class");
  }
}
