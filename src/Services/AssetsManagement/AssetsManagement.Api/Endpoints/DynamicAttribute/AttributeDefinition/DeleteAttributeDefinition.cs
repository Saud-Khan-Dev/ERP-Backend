public sealed record DeleteAttributeDefinitionResponse(bool IsSuccess);

public class DeleteAttributeDefinition : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapDelete("/attribute-definitions/{id}", async (Guid id, ISender sender) =>
    {
      var result = await sender.Send(new DeleteAttributeDefinitionCommand(id));

      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });

      return Results.Ok(result.Value.Adapt<DeleteAttributeDefinitionResponse>());
    })
      .WithName("DeleteAttributeDefinition")
      .Produces<DeleteAttributeDefinitionResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Delete Attribute Definition")
      .WithDescription("Hard-deletes an attribute that has no recorded values (its assignments go with it); otherwise deactivate it.");
  }
}
