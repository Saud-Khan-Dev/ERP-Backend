public sealed record DeactivateDepreciationScheduleRequest(DateOnly? EndDate = null);
public sealed record DeactivateDepreciationScheduleResponse(bool IsSuccess);

public class DeactivateDepreciationSchedule : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapPost("/depreciation-schedules/{scheduleId}/deactivate", async (Guid scheduleId, DeactivateDepreciationScheduleRequest request, ISender sender) =>
    {
      var result = await sender.Send(new DeactivateDepreciationScheduleCommand(scheduleId, request.EndDate));

      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });

      return Results.Ok(result.Value.Adapt<DeactivateDepreciationScheduleResponse>());
    })
      .WithName("DeactivateDepreciationSchedule")
      .Produces<DeactivateDepreciationScheduleResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Deactivate Depreciation Schedule")
      .WithDescription("Closes the schedule so another one can be started or the asset can reach a terminal status.");
  }
}
