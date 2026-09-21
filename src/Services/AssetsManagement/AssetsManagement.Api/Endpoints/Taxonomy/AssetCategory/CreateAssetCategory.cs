public sealed record CreateAssetCategoryRequest(AssetCategoryInput Category);
public sealed record CreateAssetCategoryResponse(Guid Id);

public class CreateAssetCategory : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapPost("/asset-categories", async (CreateAssetCategoryRequest request, ISender sender) =>
    {
      var result = await sender.Send(new CreateAssetCategoryCommand(request.Category));

      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });

      var response = result.Value.Adapt<CreateAssetCategoryResponse>();
      return Results.Created($"/asset-categories/{response!.Id}", response);
    })
      .WithName("CreateAssetCategory")
      .Produces<CreateAssetCategoryResponse>(StatusCodes.Status201Created)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Create Asset Category")
      .WithDescription("Adds a node to the category tree (PHYSICAL > IT Equipment > Computer > Laptop). Assets attach to leaf nodes only.");
  }
}
