public sealed record DeleteOptionSetResponse(bool IsSuccess);

public class DeleteOptionSet : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapDelete("/option-sets/{id}", async (Guid id, ISender sender) =>
    {
      var result = await sender.Send(new DeleteOptionSetCommand(id));

      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });

      return Results.Ok(result.Value.Adapt<DeleteOptionSetResponse>());
    })
      .WithName("DeleteOptionSet")
      .Produces<DeleteOptionSetResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Delete Option Set")
      .WithDescription("Deletes a non-system option set that no attribute definition uses.");
  }
}
