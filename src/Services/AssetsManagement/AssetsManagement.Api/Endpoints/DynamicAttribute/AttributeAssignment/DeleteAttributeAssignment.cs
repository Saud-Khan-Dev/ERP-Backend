public sealed record DeleteAttributeAssignmentResponse(bool IsSuccess);

public class DeleteAttributeAssignment : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapDelete("/attribute-assignments/{id}", async (Guid id, ISender sender) =>
    {
      var result = await sender.Send(new DeleteAttributeAssignmentCommand(id));

      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });

      return Results.Ok(result.Value.Adapt<DeleteAttributeAssignmentResponse>());
    })
      .WithName("DeleteAttributeAssignment")
      .Produces<DeleteAttributeAssignmentResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Delete Attribute Assignment")
      .WithDescription("Detaches an attribute from a scope. Values already stored on assets are kept (history preserved) but no longer validated.");
  }
}
