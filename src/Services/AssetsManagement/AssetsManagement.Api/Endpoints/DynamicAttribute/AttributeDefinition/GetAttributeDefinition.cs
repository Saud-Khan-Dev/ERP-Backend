public sealed record GetAttributeDefinitionResponse(AttributeDefinitionDto Definition, IReadOnlyList<AttributeAssignmentDto> Assignments);

public class GetAttributeDefinition : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapGet("/attribute-definitions/{id}", async (Guid id, ISender sender) =>
    {
      var result = await sender.Send(new GetAttributeDefinitionQuery(id));
      return Results.Ok(new GetAttributeDefinitionResponse(result.Value!.Definition, result.Value.Assignments));
    })
      .WithName("GetAttributeDefinition")
      .Produces<GetAttributeDefinitionResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Get Attribute Definition")
      .WithDescription("Returns the definition together with every scope it is assigned to.");
  }
}
