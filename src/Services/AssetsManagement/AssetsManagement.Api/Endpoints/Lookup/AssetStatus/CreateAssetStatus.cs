public sealed record CreateAssetStatusRequest(AssetStatusInput Status);
public sealed record CreateAssetStatusResponse(Guid Id);

public class CreateAssetStatus : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapPost("/asset-statuses", async (CreateAssetStatusRequest request, ISender sender) =>
    {
      var result = await sender.Send(new CreateAssetStatusCommand(request.Status));

      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });

      var response = result.Value.Adapt<CreateAssetStatusResponse>();
      return Results.Created($"/asset-statuses/{response!.Id}", response);
    })
      .WithName("CreateAssetStatus")
      .Produces<CreateAssetStatusResponse>(StatusCodes.Status201Created)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Create Asset Status")
      .WithDescription("Create Asset Status");
  }
}
