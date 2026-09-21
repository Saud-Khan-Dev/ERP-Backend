public sealed record UpdateAssetClassRequest(AssetClassInput AssetClass);
public sealed record UpdateAssetClassResponse(bool IsSuccess);

public class UpdateAssetClass : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapPut("/asset-classes/{id}", async (Guid id, UpdateAssetClassRequest request, ISender sender) =>
    {
      var result = await sender.Send(new UpdateAssetClassCommand(id, request.AssetClass));

      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });

      return Results.Ok(result.Value.Adapt<UpdateAssetClassResponse>());
    })
      .WithName("UpdateAssetClass")
      .Produces<UpdateAssetClassResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Update Asset Class")
      .WithDescription("Update Asset Class");
  }
}
