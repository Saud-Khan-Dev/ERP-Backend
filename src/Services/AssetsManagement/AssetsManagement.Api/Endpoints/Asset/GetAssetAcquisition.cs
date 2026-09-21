public sealed record GetAssetAcquisitionResponse(AssetAcquisitionDto Acquisition);

public class GetAssetAcquisition : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapGet("/assets/{id}/acquisition", async (Guid id, ISender sender) =>
    {
      var result = await sender.Send(new GetAssetAcquisitionQuery(id));
      return Results.Ok(new GetAssetAcquisitionResponse(result.Value!.Acquisition));
    })
      .WithName("GetAssetAcquisition")
      .Produces<GetAssetAcquisitionResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Get Asset Acquisition")
      .WithDescription("Get Asset Acquisition");
  }
}
