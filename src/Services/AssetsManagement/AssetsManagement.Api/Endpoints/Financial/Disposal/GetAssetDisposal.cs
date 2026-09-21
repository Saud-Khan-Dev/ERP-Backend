public sealed record GetAssetDisposalResponse(AssetDisposalDto Disposal);

public class GetAssetDisposal : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapGet("/assets/{id}/disposal", async (Guid id, ISender sender) =>
    {
      var result = await sender.Send(new GetAssetDisposalQuery(id));
      return Results.Ok(new GetAssetDisposalResponse(result.Value!.Disposal));
    })
      .WithName("GetAssetDisposal")
      .Produces<GetAssetDisposalResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Get Asset Disposal")
      .WithDescription("Get Asset Disposal");
  }
}
