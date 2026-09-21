public sealed record CreateDepreciationScheduleRequest(Guid MethodId, int UsefulLifeMonths, decimal SalvageValue, DateOnly StartDate, decimal? DecliningRate = null);
public sealed record CreateDepreciationScheduleResponse(Guid Id);

public class CreateDepreciationSchedule : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapPost("/assets/{id}/depreciation-schedules", async (Guid id, CreateDepreciationScheduleRequest request, ISender sender) =>
    {
      var result = await sender.Send(new CreateDepreciationScheduleCommand(id, request.MethodId, request.UsefulLifeMonths, request.SalvageValue, request.StartDate, request.DecliningRate));

      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });

      var response = result.Value.Adapt<CreateDepreciationScheduleResponse>();
      return Results.Created($"/depreciation-schedules/{response!.Id}", response);
    })
      .WithName("CreateDepreciationSchedule")
      .Produces<CreateDepreciationScheduleResponse>(StatusCodes.Status201Created)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Create Depreciation Schedule")
      .WithDescription("Starts depreciating the asset (one active schedule per asset). Requires an acquisition record and a depreciable asset type.");
  }
}
