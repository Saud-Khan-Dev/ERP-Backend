public sealed record GenerateDepreciationEntriesRequest(DateOnly? Until = null);
public sealed record GenerateDepreciationEntriesResponse(IReadOnlyList<AssetDepreciationEntryDto> Entries);

public class GenerateDepreciationEntries : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapPost("/depreciation-schedules/{scheduleId}/entries/generate", async (Guid scheduleId, GenerateDepreciationEntriesRequest request, ISender sender) =>
    {
      var result = await sender.Send(new GenerateDepreciationEntriesCommand(scheduleId, request.Until));

      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });

      return Results.Ok(new GenerateDepreciationEntriesResponse(result.Value!.Entries));
    })
      .WithName("GenerateDepreciationEntries")
      .Produces<GenerateDepreciationEntriesResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Generate Depreciation Entries")
      .WithDescription("Generates the missing monthly entries up to a date (STRAIGHT_LINE / DECLINING_BALANCE). Entries are created unposted.");
  }
}
