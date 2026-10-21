using System.Text.Json;

public sealed record RecordLifecycleEventRequest(Guid EventTypeId, DateTime? EventDate = null, string? Notes = null, JsonElement? Details = null);
public sealed record RecordLifecycleEventResponse(Guid Id);

public class RecordLifecycleEvent : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapPost("/assets/{id}/lifecycle-events", async (Guid id, RecordLifecycleEventRequest request, ISender sender) =>
    {
      var result = await sender.Send(new RecordLifecycleEventCommand(id, request.EventTypeId, request.EventDate, request.Notes, request.Details));

      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });

      var response = result.Value.Adapt<RecordLifecycleEventResponse>();
      return Results.Created($"/assets/{id}/lifecycle-events/{response!.Id}", response);
    })
      .RequirePermission(PermissionCatalog.Assets.Edit)

      .WithName("RecordLifecycleEvent")
      .Produces<RecordLifecycleEventResponse>(StatusCodes.Status201Created)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Record Lifecycle Event")
      .WithDescription("Adds an entry recorded by hand (maintenance, inspection ...) with optional details. The events the acts write themselves (registration, moves, status changes, disposal) are refused here.");
  }
}
