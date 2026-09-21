public sealed record CreateLifecycleEventTypeRequest(LifecycleEventTypeInput EventType);
public sealed record CreateLifecycleEventTypeResponse(Guid Id);

public class CreateLifecycleEventType : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapPost("/lifecycle-event-types", async (CreateLifecycleEventTypeRequest request, ISender sender) =>
    {
      var result = await sender.Send(new CreateLifecycleEventTypeCommand(request.EventType));

      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });

      var response = result.Value.Adapt<CreateLifecycleEventTypeResponse>();
      return Results.Created($"/lifecycle-event-types/{response!.Id}", response);
    })
      .WithName("CreateLifecycleEventType")
      .Produces<CreateLifecycleEventTypeResponse>(StatusCodes.Status201Created)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Create Lifecycle Event Type")
      .WithDescription("Create Lifecycle Event Type");
  }
}
