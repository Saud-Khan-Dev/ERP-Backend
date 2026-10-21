/// The acting person comes from the sign-in token; the timeline line is written as a "Status changed" event.
public sealed record ChangeAssetStatusRequest(Guid ToStatusId, string? Notes = null, DateTime? EventDate = null);
public sealed record ChangeAssetStatusResponse(Guid LifecycleEventId);

public class ChangeAssetStatus : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapPost("/assets/{id}/status", async (Guid id, ChangeAssetStatusRequest request, ISender sender) =>
    {
      var result = await sender.Send(new ChangeAssetStatusCommand(id, request.ToStatusId, request.Notes, request.EventDate));

      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });

      return Results.Ok(result.Value.Adapt<ChangeAssetStatusResponse>());
    })
      .RequirePermission(PermissionCatalog.Assets.Edit)

      .WithName("ChangeAssetStatus")
      .Produces<ChangeAssetStatusResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Change Asset Status")
      .WithDescription("Moves the asset to another status and records a lifecycle event. Statuses that end the record (disposed, written off) are refused here: those go through the disposal act. Refused on a disposed asset.");
  }
}
