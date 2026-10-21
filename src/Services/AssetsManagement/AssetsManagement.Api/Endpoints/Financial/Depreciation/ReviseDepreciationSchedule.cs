public sealed record ReviseDepreciationScheduleRequest(int RemainingLifeMonths, decimal SalvageValue, Guid? MethodId = null, decimal? DecliningRate = null, string? Reason = null);

public class ReviseDepreciationSchedule : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapPost("/depreciation-schedules/{scheduleId}/revise", async (Guid scheduleId, ReviseDepreciationScheduleRequest request, ISender sender) =>
    {
      var result = await sender.Send(new ReviseDepreciationScheduleCommand(
        scheduleId, request.RemainingLifeMonths, request.SalvageValue, request.MethodId, request.DecliningRate, request.Reason));

      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });

      return Results.Created($"/depreciation-schedules/{result.Value!.ScheduleId}", result.Value);
    })
      .RequirePermission(PermissionCatalog.AssetFinance.Edit)

      .WithName("ReviseDepreciationSchedule")
      .Produces<ReviseDepreciationScheduleCommandResult>(StatusCodes.Status201Created)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Revise Useful Life")
      .WithDescription("A change of estimate: the running schedule stops after its last posted month and a new one spreads the book value left (cost - every posted month not reversed) less the new salvage value over remainingLifeMonths, by the same or another method. Posted months never change. Refused on a disposed asset.");
  }
}
