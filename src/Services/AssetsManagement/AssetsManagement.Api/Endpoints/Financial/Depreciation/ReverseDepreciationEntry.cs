public sealed record ReverseDepreciationEntryRequest(Guid? ReversedBy = null);
public sealed record ReverseDepreciationEntryResponse(bool IsSuccess);

public class ReverseDepreciationEntry : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapPost("/depreciation-schedules/{scheduleId}/entries/{entryId}/reverse", async (Guid scheduleId, Guid entryId, ReverseDepreciationEntryRequest request, ISender sender) =>
    {
      var result = await sender.Send(new ReverseDepreciationEntryCommand(scheduleId, entryId, request.ReversedBy));

      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });

      return Results.Ok(result.Value.Adapt<ReverseDepreciationEntryResponse>());
    })
      .WithName("ReverseDepreciationEntry")
      .Produces<ReverseDepreciationEntryResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Reverse Depreciation Entry")
      .WithDescription("Reverses a posted entry (audit trail is kept).");
  }
}
