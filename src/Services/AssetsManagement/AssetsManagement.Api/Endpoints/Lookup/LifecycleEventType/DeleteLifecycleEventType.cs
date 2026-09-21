public sealed record DeleteLifecycleEventTypeResponse(bool IsSuccess);

public class DeleteLifecycleEventType : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapDelete("/lifecycle-event-types/{id}", async (Guid id, ISender sender) =>
    {
      var result = await sender.Send(new DeleteLifecycleEventTypeCommand(id));

      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });

      return Results.Ok(result.Value.Adapt<DeleteLifecycleEventTypeResponse>());
    })
      .WithName("DeleteLifecycleEventType")
      .Produces<DeleteLifecycleEventTypeResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Delete Lifecycle Event Type")
      .WithDescription("Deletes an unused Lifecycle Event Type; referenced rows must be deactivated instead.");
  }
}
