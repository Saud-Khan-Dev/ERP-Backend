public sealed record DeactivateDepreciationScheduleRequest(DateOnly? LastDay = null);
public sealed record DeactivateDepreciationScheduleResponse(bool IsSuccess);

public class DeactivateDepreciationSchedule : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapPost("/depreciation-schedules/{scheduleId}/deactivate", async (Guid scheduleId, DeactivateDepreciationScheduleRequest request, ISender sender) =>
    {
      var result = await sender.Send(new DeactivateDepreciationScheduleCommand(scheduleId, request.LastDay));

      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });

      return Results.Ok(result.Value.Adapt<DeactivateDepreciationScheduleResponse>());
    })
      .RequireFinance(PermissionCatalog.AssetFinance.Edit)

      .WithName("DeactivateDepreciationSchedule")
      .Produces<DeactivateDepreciationScheduleResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Deactivate Depreciation Schedule")
      .WithDescription("Stops the schedule: lastDay (default today) is the last day it still covers; draft months after it go, posted months stay. To change the life or the value at the end, use revise instead.");
  }
}
