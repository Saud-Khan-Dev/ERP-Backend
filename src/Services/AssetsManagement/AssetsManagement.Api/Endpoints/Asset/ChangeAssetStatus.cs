public sealed record ChangeAssetStatusRequest(Guid ToStatusId, Guid EventTypeId, string? Notes = null, Guid? PerformedBy = null, DateTime? EventDate = null);
public sealed record ChangeAssetStatusResponse(Guid LifecycleEventId);

public class ChangeAssetStatus : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapPost("/assets/{id}/status", async (Guid id, ChangeAssetStatusRequest request, ISender sender) =>
    {
      var result = await sender.Send(new ChangeAssetStatusCommand(id, request.ToStatusId, request.EventTypeId, request.Notes, request.PerformedBy, request.EventDate));

      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });

      return Results.Ok(result.Value.Adapt<ChangeAssetStatusResponse>());
    })
      .WithName("ChangeAssetStatus")
      .Produces<ChangeAssetStatusResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Change Asset Status")
      .WithDescription("Moves the asset to another status and records a lifecycle event. Terminal statuses are blocked while a depreciation schedule is active.");
  }
}
