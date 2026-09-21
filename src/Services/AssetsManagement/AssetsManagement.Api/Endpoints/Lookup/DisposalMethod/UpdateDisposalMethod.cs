public sealed record UpdateDisposalMethodRequest(DisposalMethodInput Method);
public sealed record UpdateDisposalMethodResponse(bool IsSuccess);

public class UpdateDisposalMethod : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapPut("/disposal-methods/{id}", async (Guid id, UpdateDisposalMethodRequest request, ISender sender) =>
    {
      var result = await sender.Send(new UpdateDisposalMethodCommand(id, request.Method));

      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });

      return Results.Ok(result.Value.Adapt<UpdateDisposalMethodResponse>());
    })
      .WithName("UpdateDisposalMethod")
      .Produces<UpdateDisposalMethodResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Update Disposal Method")
      .WithDescription("Update Disposal Method");
  }
}
