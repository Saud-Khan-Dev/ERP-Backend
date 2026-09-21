public sealed record AddOptionSetValueRequest(OptionSetValueInput Value);
public sealed record AddOptionSetValueResponse(Guid Id);

public class AddOptionSetValue : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapPost("/option-sets/{id}/values", async (Guid id, AddOptionSetValueRequest request, ISender sender) =>
    {
      var result = await sender.Send(new AddOptionSetValueCommand(id, request.Value));

      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });

      var response = result.Value.Adapt<AddOptionSetValueResponse>();
      return Results.Created($"/option-sets/{id}/values/{response!.Id}", response);
    })
      .WithName("AddOptionSetValue")
      .Produces<AddOptionSetValueResponse>(StatusCodes.Status201Created)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Add Option Set Value")
      .WithDescription("Adds a value (code + label) to an option set; parentValueId enables cascading dropdowns.");
  }
}
