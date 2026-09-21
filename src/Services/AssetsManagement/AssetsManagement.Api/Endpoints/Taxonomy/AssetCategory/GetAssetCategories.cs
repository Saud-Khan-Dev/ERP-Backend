public sealed record GetAssetCategoriesResponse(IReadOnlyList<AssetCategoryDto> Categories);

public class GetAssetCategories : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapGet("/asset-categories", async (
      ISender sender,
      Guid? assetClassId,
      Guid? parentCategoryId,
      bool? onlyRoots,
      bool? onlyLeaves,
      bool? includeInactive) =>
    {
      var result = await sender.Send(new GetAssetCategoriesQuery(
        assetClassId, parentCategoryId, onlyRoots ?? false, onlyLeaves ?? false, includeInactive ?? false));

      return Results.Ok(new GetAssetCategoriesResponse(result.Value!.Categories));
    })
      .WithName("GetAssetCategories")
      .Produces<GetAssetCategoriesResponse>(StatusCodes.Status200OK)
      .WithSummary("Get Asset Categories")
      .WithDescription("Lists categories: filter by class, by parent (children of a node), onlyRoots for the top level, onlyLeaves for asset-attachable nodes.");
  }
}
