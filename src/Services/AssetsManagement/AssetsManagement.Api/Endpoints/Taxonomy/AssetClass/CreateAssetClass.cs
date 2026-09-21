public sealed record CreateAssetClassRequest(AssetClassInput AssetClass);
public sealed record CreateAssetClassResponse(Guid Id);

public class CreateAssetClass : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapPost("/asset-classes", async (CreateAssetClassRequest request, ISender sender) =>
    {
      var result = await sender.Send(new CreateAssetClassCommand(request.AssetClass));

      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });

      var response = result.Value.Adapt<CreateAssetClassResponse>();
      return Results.Created($"/asset-classes/{response!.Id}", response);
    })
      .WithName("CreateAssetClass")
      .Produces<CreateAssetClassResponse>(StatusCodes.Status201Created)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Create Asset Class")
      .WithDescription("Level 1 of the taxonomy: PHYSICAL / FINANCIAL / INTANGIBLE / DIGITAL ...");
  }
}
