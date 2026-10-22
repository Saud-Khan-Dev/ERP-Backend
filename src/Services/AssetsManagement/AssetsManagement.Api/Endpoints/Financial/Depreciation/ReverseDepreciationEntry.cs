public sealed record ReverseDepreciationEntryRequest(string Reason);
public sealed record ReverseDepreciationEntryResponse(bool IsSuccess);

public class ReverseDepreciationEntry : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapPost("/depreciation-schedules/{scheduleId}/entries/{entryId}/reverse", async (Guid scheduleId, Guid entryId, ReverseDepreciationEntryRequest request, ISender sender) =>
    {
      var result = await sender.Send(new ReverseDepreciationEntryCommand(scheduleId, entryId, request.Reason));

      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });

      return Results.Ok(result.Value.Adapt<ReverseDepreciationEntryResponse>());
    })
      .RequireFinance(PermissionCatalog.AssetFinance.Reverse)

      .WithName("ReverseDepreciationEntry")
      .Produces<ReverseDepreciationEntryResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Reverse Depreciation Entry")
      .WithDescription("Reverses a posted month with a reason (body {reason}); the month and its reversal stay on record and the audit trail keeps a Reversed line. ASSET_FINANCE.REVERSE. Refused to the person who posted the month while the rule \"Depreciation is reversed by someone else\" is on.");
  }
}
