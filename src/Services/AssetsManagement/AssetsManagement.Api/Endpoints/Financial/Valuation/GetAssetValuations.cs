public sealed record GetAssetValuationsResponse(IReadOnlyList<AssetValuationDto> Valuations);

public class GetAssetValuations : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapGet("/assets/{id}/valuations", async (Guid id, ISender sender) =>
    {
      var result = await sender.Send(new GetAssetValuationsQuery(id));
      return Results.Ok(new GetAssetValuationsResponse(result.Value!.Valuations));
    })
      .WithName("GetAssetValuations")
      .Produces<GetAssetValuationsResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Get Asset Valuations")
      .WithDescription("Valuation history, newest first.");
  }
}
