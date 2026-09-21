public sealed record DeleteAssetValuationResponse(bool IsSuccess);

public class DeleteAssetValuation : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapDelete("/assets/{id}/valuations/{valuationId}", async (Guid id, Guid valuationId, ISender sender) =>
    {
      var result = await sender.Send(new DeleteAssetValuationCommand(id, valuationId));

      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });

      return Results.Ok(result.Value.Adapt<DeleteAssetValuationResponse>());
    })
      .WithName("DeleteAssetValuation")
      .Produces<DeleteAssetValuationResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Delete Asset Valuation")
      .WithDescription("Delete Asset Valuation");
  }
}
