public sealed record CreateLocationRequest(LocationInput Location);
public sealed record CreateLocationResponse(Guid Id);

public class CreateLocation : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapPost("/locations", async (CreateLocationRequest request, ISender sender) =>
    {
      var result = await sender.Send(new CreateLocationCommand(request.Location));

      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });

      var response = result.Value.Adapt<CreateLocationResponse>();
      return Results.Created($"/locations/{response!.Id}", response);
    })
      .WithName("CreateLocation")
      .Produces<CreateLocationResponse>(StatusCodes.Status201Created)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Create Location")
      .WithDescription("Create Location");
  }
}
