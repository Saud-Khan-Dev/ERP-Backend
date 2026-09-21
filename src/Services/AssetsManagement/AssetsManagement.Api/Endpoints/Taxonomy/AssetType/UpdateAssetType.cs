public sealed record UpdateAssetTypeRequest(AssetTypeInput AssetType);
public sealed record UpdateAssetTypeResponse(bool IsSuccess);

public class UpdateAssetType : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapPut("/asset-types/{id}", async (Guid id, UpdateAssetTypeRequest request, ISender sender) =>
    {
      var result = await sender.Send(new UpdateAssetTypeCommand(id, request.AssetType));

      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });

      return Results.Ok(result.Value.Adapt<UpdateAssetTypeResponse>());
    })
      .WithName("UpdateAssetType")
      .Produces<UpdateAssetTypeResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Update Asset Type")
      .WithDescription("Update Asset Type");
  }
}
