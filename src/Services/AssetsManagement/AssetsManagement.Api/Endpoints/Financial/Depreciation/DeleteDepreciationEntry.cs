public sealed record DeleteDepreciationEntryResponse(bool IsSuccess);

public class DeleteDepreciationEntry : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapDelete("/depreciation-schedules/{scheduleId}/entries/{entryId}", async (Guid scheduleId, Guid entryId, ISender sender) =>
    {
      var result = await sender.Send(new DeleteDepreciationEntryCommand(scheduleId, entryId));

      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });

      return Results.Ok(result.Value.Adapt<DeleteDepreciationEntryResponse>());
    })
      .WithName("DeleteDepreciationEntry")
      .Produces<DeleteDepreciationEntryResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Delete Depreciation Entry")
      .WithDescription("Deletes an unposted entry.");
  }
}
