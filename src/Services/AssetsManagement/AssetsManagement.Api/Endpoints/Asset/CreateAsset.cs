public sealed record CreateAssetRequest(CreateAssetInput Asset, Guid? PerformedBy = null);
public sealed record CreateAssetResponse(Guid Id);

public class CreateAsset : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapPost("/assets", async (CreateAssetRequest request, ISender sender) =>
    {
      var result = await sender.Send(new CreateAssetCommand(request.Asset, request.PerformedBy));

      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });

      var response = result.Value.Adapt<CreateAssetResponse>();
      return Results.Created($"/assets/{response!.Id}", response);
    })
      .WithName("CreateAsset")
      .Produces<CreateAssetResponse>(StatusCodes.Status201Created)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Create Asset")
      .WithDescription("Creates an asset on a leaf category. extraAttributes is a JSON object keyed by attribute code and is validated against the resolved schema (GET /attribute-schema).");
  }
}
