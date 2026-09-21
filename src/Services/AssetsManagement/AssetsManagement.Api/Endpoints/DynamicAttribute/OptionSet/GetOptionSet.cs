public sealed record GetOptionSetResponse(OptionSetDto OptionSet);

public class GetOptionSet : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapGet("/option-sets/{id}", async (Guid id, ISender sender) =>
    {
      var result = await sender.Send(new GetOptionSetQuery(id));
      return Results.Ok(new GetOptionSetResponse(result.Value!.OptionSet));
    })
      .WithName("GetOptionSet")
      .Produces<GetOptionSetResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Get Option Set")
      .WithDescription("Returns the option set with all its values.");
  }
}
