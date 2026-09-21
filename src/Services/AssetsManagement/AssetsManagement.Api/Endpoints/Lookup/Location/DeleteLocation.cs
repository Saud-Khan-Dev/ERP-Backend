public sealed record DeleteLocationResponse(bool IsSuccess);

public class DeleteLocation : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapDelete("/locations/{id}", async (Guid id, ISender sender) =>
    {
      var result = await sender.Send(new DeleteLocationCommand(id));

      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });

      return Results.Ok(result.Value.Adapt<DeleteLocationResponse>());
    })
      .WithName("DeleteLocation")
      .Produces<DeleteLocationResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Delete Location")
      .WithDescription("Deletes an unused Location; referenced rows must be deactivated instead.");
  }
}
