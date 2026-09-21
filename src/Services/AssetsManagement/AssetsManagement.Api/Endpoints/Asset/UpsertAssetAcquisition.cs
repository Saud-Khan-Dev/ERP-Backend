public sealed record UpsertAssetAcquisitionRequest(AssetAcquisitionInput Acquisition);
public sealed record UpsertAssetAcquisitionResponse(Guid Id, bool Created);

public class UpsertAssetAcquisition : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapPut("/assets/{id}/acquisition", async (Guid id, UpsertAssetAcquisitionRequest request, ISender sender) =>
    {
      var result = await sender.Send(new UpsertAssetAcquisitionCommand(id, request.Acquisition));

      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });

      var response = result.Value.Adapt<UpsertAssetAcquisitionResponse>();
      return response!.Created ? Results.Created($"/assets/{id}/acquisition", response) : Results.Ok(response);
    })
      .WithName("UpsertAssetAcquisition")
      .Produces<UpsertAssetAcquisitionResponse>(StatusCodes.Status200OK)
      .Produces<UpsertAssetAcquisitionResponse>(StatusCodes.Status201Created)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Upsert Asset Acquisition")
      .WithDescription("Records (or updates) how and for how much the asset was acquired, including warranty dates.");
  }
}
