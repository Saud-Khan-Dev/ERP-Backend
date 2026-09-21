public sealed record UpdateAssetCategoryRequest(AssetCategoryInput Category);
public sealed record UpdateAssetCategoryResponse(bool IsSuccess);

public class UpdateAssetCategory : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapPut("/asset-categories/{id}", async (Guid id, UpdateAssetCategoryRequest request, ISender sender) =>
    {
      var result = await sender.Send(new UpdateAssetCategoryCommand(id, request.Category));

      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });

      return Results.Ok(result.Value.Adapt<UpdateAssetCategoryResponse>());
    })
      .WithName("UpdateAssetCategory")
      .Produces<UpdateAssetCategoryResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Update Asset Category")
      .WithDescription("Updates a category; changing parentCategoryId moves the node and rebuilds the subtree paths.");
  }
}
