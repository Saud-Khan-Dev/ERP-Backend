public sealed record CreateOptionSetRequest(OptionSetInput OptionSet);
public sealed record CreateOptionSetResponse(Guid Id);

public class CreateOptionSet : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapPost("/option-sets", async (CreateOptionSetRequest request, ISender sender) =>
    {
      var result = await sender.Send(new CreateOptionSetCommand(request.OptionSet));

      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });

      var response = result.Value.Adapt<CreateOptionSetResponse>();
      return Results.Created($"/option-sets/{response!.Id}", response);
    })
      .WithName("CreateOptionSet")
      .Produces<CreateOptionSetResponse>(StatusCodes.Status201Created)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Create Option Set")
      .WithDescription("Reusable dropdown list (e.g. OPERATING_SYSTEM) with optional initial values.");
  }
}
