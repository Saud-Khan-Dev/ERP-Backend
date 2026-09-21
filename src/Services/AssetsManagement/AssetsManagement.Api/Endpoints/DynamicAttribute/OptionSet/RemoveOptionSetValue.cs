public sealed record RemoveOptionSetValueResponse(bool IsSuccess);

public class RemoveOptionSetValue : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapDelete("/option-sets/{id}/values/{valueId}", async (Guid id, Guid valueId, ISender sender) =>
    {
      var result = await sender.Send(new RemoveOptionSetValueCommand(id, valueId));

      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });

      return Results.Ok(result.Value.Adapt<RemoveOptionSetValueResponse>());
    })
      .WithName("RemoveOptionSetValue")
      .Produces<RemoveOptionSetValueResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Remove Option Set Value")
      .WithDescription("Removes an unused value; values referenced by assets must be deactivated instead.");
  }
}
