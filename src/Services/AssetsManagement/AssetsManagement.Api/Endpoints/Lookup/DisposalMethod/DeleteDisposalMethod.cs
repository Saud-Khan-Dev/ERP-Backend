public sealed record DeleteDisposalMethodResponse(bool IsSuccess);

public class DeleteDisposalMethod : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapDelete("/disposal-methods/{id}", async (Guid id, ISender sender) =>
    {
      var result = await sender.Send(new DeleteDisposalMethodCommand(id));

      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });

      return Results.Ok(result.Value.Adapt<DeleteDisposalMethodResponse>());
    })
      .WithName("DeleteDisposalMethod")
      .Produces<DeleteDisposalMethodResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Delete Disposal Method")
      .WithDescription("Deletes an unused Disposal Method; referenced rows must be deactivated instead.");
  }
}
