public sealed record CreateAttributeDefinitionRequest(AttributeDefinitionInput Definition);
public sealed record CreateAttributeDefinitionResponse(Guid Id);

public class CreateAttributeDefinition : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapPost("/attribute-definitions", async (CreateAttributeDefinitionRequest request, ISender sender) =>
    {
      var result = await sender.Send(new CreateAttributeDefinitionCommand(request.Definition));

      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });

      var response = result.Value.Adapt<CreateAttributeDefinitionResponse>();
      return Results.Created($"/attribute-definitions/{response!.Id}", response);
    })
      .WithName("CreateAttributeDefinition")
      .Produces<CreateAttributeDefinitionResponse>(StatusCodes.Status201Created)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Create Attribute Definition")
      .WithDescription("Defines a reusable dynamic field (code + data type + validation). Attach it to a class / type / category / asset with an attribute assignment.");
  }
}
