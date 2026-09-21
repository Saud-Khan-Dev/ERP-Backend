public sealed record PostDepreciationEntryRequest(Guid? PostedBy = null);
public sealed record PostDepreciationEntryResponse(bool IsSuccess);

public class PostDepreciationEntry : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapPost("/depreciation-schedules/{scheduleId}/entries/{entryId}/post", async (Guid scheduleId, Guid entryId, PostDepreciationEntryRequest request, ISender sender) =>
    {
      var result = await sender.Send(new PostDepreciationEntryCommand(scheduleId, entryId, request.PostedBy));

      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });

      return Results.Ok(result.Value.Adapt<PostDepreciationEntryResponse>());
    })
      .WithName("PostDepreciationEntry")
      .Produces<PostDepreciationEntryResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Post Depreciation Entry")
      .WithDescription("Marks the entry as posted to the ledger.");
  }
}
