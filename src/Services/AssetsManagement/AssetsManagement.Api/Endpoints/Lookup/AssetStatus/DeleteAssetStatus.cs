public sealed record DeleteAssetStatusResponse(bool IsSuccess);

public class DeleteAssetStatus : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapDelete("/asset-statuses/{id}", async (Guid id, ISender sender) =>
    {
      var result = await sender.Send(new DeleteAssetStatusCommand(id));

      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });

      return Results.Ok(result.Value.Adapt<DeleteAssetStatusResponse>());
    })
      .WithName("DeleteAssetStatus")
      .Produces<DeleteAssetStatusResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Delete Asset Status")
      .WithDescription("Deletes an unused Asset Status; referenced rows must be deactivated instead.");
  }
}
