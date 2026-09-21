public sealed record DeleteAssetClassResponse(bool IsSuccess);

public class DeleteAssetClass : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapDelete("/asset-classes/{id}", async (Guid id, ISender sender) =>
    {
      var result = await sender.Send(new DeleteAssetClassCommand(id));

      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });

      return Results.Ok(result.Value.Adapt<DeleteAssetClassResponse>());
    })
      .WithName("DeleteAssetClass")
      .Produces<DeleteAssetClassResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Delete Asset Class")
      .WithDescription("Deletes an unused asset class; classes referenced by types, categories or assets must be deactivated instead.");
  }
}
