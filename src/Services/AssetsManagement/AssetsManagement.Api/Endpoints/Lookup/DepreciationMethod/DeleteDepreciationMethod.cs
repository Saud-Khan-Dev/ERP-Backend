public sealed record DeleteDepreciationMethodResponse(bool IsSuccess);

public class DeleteDepreciationMethod : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapDelete("/depreciation-methods/{id}", async (Guid id, ISender sender) =>
    {
      var result = await sender.Send(new DeleteDepreciationMethodCommand(id));

      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });

      return Results.Ok(result.Value.Adapt<DeleteDepreciationMethodResponse>());
    })
      .WithName("DeleteDepreciationMethod")
      .Produces<DeleteDepreciationMethodResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Delete Depreciation Method")
      .WithDescription("Deletes an unused Depreciation Method; referenced rows must be deactivated instead.");
  }
}
