public sealed record CreateAssetTypeRequest(AssetTypeInput AssetType);
public sealed record CreateAssetTypeResponse(Guid Id);

public class CreateAssetType : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapPost("/asset-types", async (CreateAssetTypeRequest request, ISender sender) =>
    {
      var result = await sender.Send(new CreateAssetTypeCommand(request.AssetType));

      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });

      var response = result.Value.Adapt<CreateAssetTypeResponse>();
      return Results.Created($"/asset-types/{response!.Id}", response);
    })
      .WithName("CreateAssetType")
      .Produces<CreateAssetTypeResponse>(StatusCodes.Status201Created)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Create Asset Type")
      .WithDescription("Level 2 of the taxonomy: MOVABLE / IMMOVABLE / FLEET under PHYSICAL, INVESTMENT under FINANCIAL ...");
  }
}
