public sealed record GetAssetLifecycleEventsResponse(IReadOnlyList<AssetLifecycleEventDto> Events);

public class GetAssetLifecycleEvents : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapGet("/assets/{id}/lifecycle-events", async (Guid id, ISender sender, Guid? eventTypeId) =>
    {
      var result = await sender.Send(new GetAssetLifecycleEventsQuery(id, eventTypeId));
      return Results.Ok(new GetAssetLifecycleEventsResponse(result.Value!.Events));
    })
      .WithName("GetAssetLifecycleEvents")
      .Produces<GetAssetLifecycleEventsResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Get Asset Lifecycle Events")
      .WithDescription("Get Asset Lifecycle Events");
  }
}
