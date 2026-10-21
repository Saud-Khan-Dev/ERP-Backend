public sealed record ReturnAssetAssignmentRequest(DateOnly? ActualReturnDate = null, string? Notes = null);
public sealed record ReturnAssetAssignmentResponse(bool IsSuccess);

public class ReturnAssetAssignment : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapPost("/assets/{id}/assignments/{assignmentId}/return", async (Guid id, Guid assignmentId, ReturnAssetAssignmentRequest request, ISender sender) =>
    {
      var result = await sender.Send(new ReturnAssetAssignmentCommand(id, assignmentId, request.ActualReturnDate, request.Notes));

      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });

      return Results.Ok(result.Value.Adapt<ReturnAssetAssignmentResponse>());
    })
      .RequirePermission(PermissionCatalog.Assets.Edit)

      .WithName("ReturnAssetAssignment")
      .Produces<ReturnAssetAssignmentResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Return Asset Assignment")
      .WithDescription("Closes a loan and moves the asset back to where it came from. A loan can always be returned, whatever the status.");
  }
}
