public sealed record DeleteAssetTypeResponse(bool IsSuccess);

public class DeleteAssetType : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapDelete("/asset-types/{id}", async (Guid id, ISender sender) =>
    {
      var result = await sender.Send(new DeleteAssetTypeCommand(id));

      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });

      return Results.Ok(result.Value.Adapt<DeleteAssetTypeResponse>());
    })
      .WithName("DeleteAssetType")
      .Produces<DeleteAssetTypeResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Delete Asset Type")
      .WithDescription("Delete Asset Type");
  }
}
