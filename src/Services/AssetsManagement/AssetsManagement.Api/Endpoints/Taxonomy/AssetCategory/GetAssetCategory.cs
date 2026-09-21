public sealed record GetAssetCategoryResponse(AssetCategoryDto Category, IReadOnlyList<AssetCategoryDto> Ancestors);

public class GetAssetCategory : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapGet("/asset-categories/{id}", async (Guid id, ISender sender) =>
    {
      var result = await sender.Send(new GetAssetCategoryQuery(id));
      return Results.Ok(new GetAssetCategoryResponse(result.Value!.Category, result.Value.Ancestors));
    })
      .WithName("GetAssetCategory")
      .Produces<GetAssetCategoryResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Get Asset Category")
      .WithDescription("Returns the category with its ancestor chain (root first).");
  }
}
