public sealed record GetLifecycleEventTypesResponse(IReadOnlyList<LifecycleEventTypeDto> EventTypes);

public class GetLifecycleEventTypes : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapGet("/lifecycle-event-types", async (ISender sender, string? stage, bool? includeInactive) =>
    {
      var result = await sender.Send(new GetLifecycleEventTypesQuery(stage, includeInactive ?? false));
      return Results.Ok(new GetLifecycleEventTypesResponse(result.Value!.EventTypes));
    })
      .WithName("GetLifecycleEventTypes")
      .Produces<GetLifecycleEventTypesResponse>(StatusCodes.Status200OK)
      .WithSummary("Get Lifecycle Event Types")
      .WithDescription("Lists event types, optionally for one stage (ACQUISITION / ASSIGNMENT / MAINTENANCE / DEPRECIATION / VALUATION / DISPOSAL).");
  }
}
