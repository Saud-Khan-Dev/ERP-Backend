public sealed record DeleteAssetCategoryResponse(bool IsSuccess);

public class DeleteAssetCategory : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapDelete("/asset-categories/{id}", async (Guid id, ISender sender) =>
    {
      var result = await sender.Send(new DeleteAssetCategoryCommand(id));

      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });

      return Results.Ok(result.Value.Adapt<DeleteAssetCategoryResponse>());
    })
      .WithName("DeleteAssetCategory")
      .Produces<DeleteAssetCategoryResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Delete Asset Category")
      .WithDescription("Deletes a leaf category that has no assets.");
  }
}
