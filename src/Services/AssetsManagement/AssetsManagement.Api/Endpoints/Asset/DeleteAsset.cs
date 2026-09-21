public sealed record DeleteAssetResponse(bool IsSuccess);

public class DeleteAsset : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapDelete("/assets/{id}", async (Guid id, ISender sender, string? deletedBy) =>
    {
      var result = await sender.Send(new DeleteAssetCommand(id, deletedBy));

      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });

      return Results.Ok(result.Value.Adapt<DeleteAssetResponse>());
    })
      .WithName("DeleteAsset")
      .Produces<DeleteAssetResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Delete Asset")
      .WithDescription("Soft-deletes the asset; depreciation, valuation and disposal history are preserved.");
  }
}
