public sealed record GetLifecycleTimelineResponse(PaginatedResult<LifecycleTimelineItemDto> Events);
public sealed record RunDepreciationRequest(DateOnly? Until, bool Post = false, Guid? PostedBy = null, Guid? AssetId = null);

/// The asset lifecycle across the whole register: the timeline of every asset's events, and the period-end depreciation run.
public class LifecycleEndpoints : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapGet("/lifecycle-events", async (
      ISender sender, int? pageIndex, int? pageSize, Guid? assetId, Guid? eventTypeId, string? stage, DateTime? from, DateTime? to) =>
    {
      var result = await sender.Send(new GetLifecycleTimelineQuery(
        new PaginationRequest(pageIndex ?? 0, Math.Clamp(pageSize ?? 20, 1, 200)), assetId, eventTypeId, stage, from, to));
      return Results.Ok(new GetLifecycleTimelineResponse(result.Value!.Events));
    })
      .WithName("GetLifecycleTimeline")
      .Produces<GetLifecycleTimelineResponse>(StatusCodes.Status200OK)
      .WithSummary("Get Lifecycle Timeline")
      .WithDescription("Lifecycle events of every asset, newest first, with the asset's code and name and the event type. Filters: assetId, eventTypeId, stage (event type stage), from / to (UTC).");

    app.MapPost("/depreciation-runs", async (RunDepreciationRequest request, ISender sender) =>
    {
      var result = await sender.Send(new RunDepreciationCommand(request.Until, request.Post, request.PostedBy, request.AssetId));
      return Results.Ok(result.Value);
    })
      .WithName("RunDepreciation")
      .Produces<RunDepreciationCommandResult>(StatusCodes.Status200OK)
      .WithSummary("Run Depreciation")
      .WithDescription("Period-end run over every active schedule: generates the missing monthly entries up to `until` (default today) and, with post=true, posts every unposted entry up to that date. assetId limits the run to one asset. Schedules that cannot run are listed in `skipped`.");
  }
}
