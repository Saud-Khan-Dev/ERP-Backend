public sealed record PostDepreciationEntryResponse(bool IsSuccess);

public class PostDepreciationEntry : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapPost("/depreciation-schedules/{scheduleId}/entries/{entryId}/post", async (Guid scheduleId, Guid entryId, ISender sender) =>
    {
      var result = await sender.Send(new PostDepreciationEntryCommand(scheduleId, entryId));

      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });

      return Results.Ok(result.Value.Adapt<PostDepreciationEntryResponse>());
    })
      .RequirePermission(PermissionCatalog.AssetFinance.Post)

      .WithName("PostDepreciationEntry")
      .Produces<PostDepreciationEntryResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Post Depreciation Entry")
      .WithDescription("Makes the month final. Refused for a month that has not ended yet and on a disposed asset.");
  }
}
