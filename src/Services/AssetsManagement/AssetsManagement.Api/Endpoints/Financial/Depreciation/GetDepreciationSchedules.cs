public sealed record GetDepreciationSchedulesResponse(IReadOnlyList<AssetDepreciationScheduleDto> Schedules, decimal? NetBookValue);

public class GetDepreciationSchedules : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapGet("/assets/{id}/depreciation-schedules", async (Guid id, ISender sender, bool? includeInactive) =>
    {
      var result = await sender.Send(new GetDepreciationSchedulesQuery(id, includeInactive ?? false));
      return Results.Ok(new GetDepreciationSchedulesResponse(result.Value!.Schedules, result.Value.NetBookValue));
    })
      .WithName("GetDepreciationSchedules")
      .Produces<GetDepreciationSchedulesResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Get Depreciation Schedules")
      .WithDescription("Schedules with their entries plus the current net book value (cost minus posted depreciation).");
  }
}
