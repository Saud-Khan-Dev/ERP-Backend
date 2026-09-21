using System.Text.Json;

public sealed record RecordLifecycleEventRequest(Guid EventTypeId, DateTime? EventDate = null, Guid? PerformedBy = null, string? Notes = null, JsonElement? Details = null);
public sealed record RecordLifecycleEventResponse(Guid Id);

public class RecordLifecycleEvent : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapPost("/assets/{id}/lifecycle-events", async (Guid id, RecordLifecycleEventRequest request, ISender sender) =>
    {
      var result = await sender.Send(new RecordLifecycleEventCommand(id, request.EventTypeId, request.EventDate, request.PerformedBy, request.Notes, request.Details));

      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });

      var response = result.Value.Adapt<RecordLifecycleEventResponse>();
      return Results.Created($"/assets/{id}/lifecycle-events/{response!.Id}", response);
    })
      .WithName("RecordLifecycleEvent")
      .Produces<RecordLifecycleEventResponse>(StatusCodes.Status201Created)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Record Lifecycle Event")
      .WithDescription("Adds a lifecycle entry (maintenance, inspection ...) with an optional JSON details payload.");
  }
}
