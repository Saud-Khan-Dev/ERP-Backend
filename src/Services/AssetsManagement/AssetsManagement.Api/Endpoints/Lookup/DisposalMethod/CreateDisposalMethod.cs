public sealed record CreateDisposalMethodRequest(DisposalMethodInput Method);
public sealed record CreateDisposalMethodResponse(Guid Id);

public class CreateDisposalMethod : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapPost("/disposal-methods", async (CreateDisposalMethodRequest request, ISender sender) =>
    {
      var result = await sender.Send(new CreateDisposalMethodCommand(request.Method));

      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });

      var response = result.Value.Adapt<CreateDisposalMethodResponse>();
      return Results.Created($"/disposal-methods/{response!.Id}", response);
    })
      .WithName("CreateDisposalMethod")
      .Produces<CreateDisposalMethodResponse>(StatusCodes.Status201Created)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Create Disposal Method")
      .WithDescription("Create Disposal Method");
  }
}
