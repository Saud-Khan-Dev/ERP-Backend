public sealed record UpdateLifecycleEventTypeRequest(LifecycleEventTypeInput EventType);
public sealed record UpdateLifecycleEventTypeResponse(bool IsSuccess);

public class UpdateLifecycleEventType : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapPut("/lifecycle-event-types/{id}", async (Guid id, UpdateLifecycleEventTypeRequest request, ISender sender) =>
    {
      var result = await sender.Send(new UpdateLifecycleEventTypeCommand(id, request.EventType));

      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });

      return Results.Ok(result.Value.Adapt<UpdateLifecycleEventTypeResponse>());
    })
      .WithName("UpdateLifecycleEventType")
      .Produces<UpdateLifecycleEventTypeResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Update Lifecycle Event Type")
      .WithDescription("Update Lifecycle Event Type");
  }
}
