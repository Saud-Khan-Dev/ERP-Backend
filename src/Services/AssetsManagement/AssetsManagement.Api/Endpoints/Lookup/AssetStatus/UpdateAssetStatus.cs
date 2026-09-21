public sealed record UpdateAssetStatusRequest(AssetStatusInput Status);
public sealed record UpdateAssetStatusResponse(bool IsSuccess);

public class UpdateAssetStatus : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapPut("/asset-statuses/{id}", async (Guid id, UpdateAssetStatusRequest request, ISender sender) =>
    {
      var result = await sender.Send(new UpdateAssetStatusCommand(id, request.Status));

      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });

      return Results.Ok(result.Value.Adapt<UpdateAssetStatusResponse>());
    })
      .WithName("UpdateAssetStatus")
      .Produces<UpdateAssetStatusResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Update Asset Status")
      .WithDescription("Update Asset Status");
  }
}
