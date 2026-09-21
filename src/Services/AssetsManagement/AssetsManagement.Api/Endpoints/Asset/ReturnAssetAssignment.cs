public sealed record ReturnAssetAssignmentRequest(DateOnly? ActualReturnDate = null);
public sealed record ReturnAssetAssignmentResponse(bool IsSuccess);

public class ReturnAssetAssignment : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapPost("/assets/{id}/assignments/{assignmentId}/return", async (Guid id, Guid assignmentId, ReturnAssetAssignmentRequest request, ISender sender) =>
    {
      var result = await sender.Send(new ReturnAssetAssignmentCommand(id, assignmentId, request.ActualReturnDate));

      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });

      return Results.Ok(result.Value.Adapt<ReturnAssetAssignmentResponse>());
    })
      .WithName("ReturnAssetAssignment")
      .Produces<ReturnAssetAssignmentResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Return Asset Assignment")
      .WithDescription("Closes a temporary assignment and moves the asset back to where it came from.");
  }
}
