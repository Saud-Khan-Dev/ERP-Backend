public sealed record AddDepreciationEntryRequest(DateOnly PeriodStart, DateOnly PeriodEnd, decimal DepreciationAmount);
public sealed record AddDepreciationEntryResponse(Guid Id);

public class AddDepreciationEntry : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapPost("/depreciation-schedules/{scheduleId}/entries", async (Guid scheduleId, AddDepreciationEntryRequest request, ISender sender) =>
    {
      var result = await sender.Send(new AddDepreciationEntryCommand(scheduleId, request.PeriodStart, request.PeriodEnd, request.DepreciationAmount));

      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });

      var response = result.Value.Adapt<AddDepreciationEntryResponse>();
      return Results.Created($"/depreciation-schedules/{scheduleId}/entries/{response!.Id}", response);
    })
      .WithName("AddDepreciationEntry")
      .Produces<AddDepreciationEntryResponse>(StatusCodes.Status201Created)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Add Depreciation Entry")
      .WithDescription("Manual entry for a period (e.g. UNITS_OF_PRODUCTION).");
  }
}
