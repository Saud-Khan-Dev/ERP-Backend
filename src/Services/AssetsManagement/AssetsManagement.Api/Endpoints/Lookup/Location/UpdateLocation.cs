public sealed record UpdateLocationRequest(LocationInput Location);
public sealed record UpdateLocationResponse(bool IsSuccess);

public class UpdateLocation : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapPut("/locations/{id}", async (Guid id, UpdateLocationRequest request, ISender sender) =>
    {
      var result = await sender.Send(new UpdateLocationCommand(id, request.Location));

      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });

      return Results.Ok(result.Value.Adapt<UpdateLocationResponse>());
    })
      .WithName("UpdateLocation")
      .Produces<UpdateLocationResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Update Location")
      .WithDescription("Update Location");
  }
}
