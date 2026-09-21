public sealed record UpdateAttributeDefinitionRequest(AttributeDefinitionInput Definition);
public sealed record UpdateAttributeDefinitionResponse(bool IsSuccess);

public class UpdateAttributeDefinition : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapPut("/attribute-definitions/{id}", async (Guid id, UpdateAttributeDefinitionRequest request, ISender sender) =>
    {
      var result = await sender.Send(new UpdateAttributeDefinitionCommand(id, request.Definition));

      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });

      return Results.Ok(result.Value.Adapt<UpdateAttributeDefinitionResponse>());
    })
      .WithName("UpdateAttributeDefinition")
      .Produces<UpdateAttributeDefinitionResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Update Attribute Definition")
      .WithDescription("Updates the field; code and data type are frozen once assets hold values for it.");
  }
}
