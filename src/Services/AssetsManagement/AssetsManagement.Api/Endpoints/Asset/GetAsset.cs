public sealed record GetAssetResponse(AssetDto Asset, IReadOnlyList<ResolvedAttributeDto> AttributeSchema);

public class GetAsset : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapGet("/assets/{id}", async (Guid id, ISender sender) =>
    {
      var result = await sender.Send(new GetAssetQuery(id));
      return Results.Ok(new GetAssetResponse(result.Value!.Asset, result.Value.AttributeSchema));
    })
      .WithName("GetAsset")
      .Produces<GetAssetResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Get Asset")
      .WithDescription("Returns the asset (with its extraAttributes) and the resolved attribute schema for rendering the form.");
  }
}
