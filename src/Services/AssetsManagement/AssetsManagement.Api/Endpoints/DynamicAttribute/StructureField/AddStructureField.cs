public sealed record AddStructureFieldRequest(
  AttributeScope Scope,
  Guid TargetId,
  Guid? ExistingDefinitionId,
  NewFieldInput? Field,
  FieldPlacementInput? Placement);

public class AddStructureField : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapPost("/structure-fields", async (AddStructureFieldRequest request, ISender sender) =>
    {
      var result = await sender.Send(new AddStructureFieldCommand(
        request.Scope, request.TargetId, request.ExistingDefinitionId, request.Field, request.Placement ?? new FieldPlacementInput()));

      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });

      return Results.Created($"/attribute-assignments/{result.Value!.AssignmentId}", result.Value);
    })
      .WithName("AddStructureField")
      .Produces<AddStructureFieldCommandResult>(StatusCodes.Status201Created)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Add Field To Structure Node")
      .WithDescription("Adds a field to the form of a class, type or category in one transaction: reuse an existing field (existingDefinitionId) or create one (field: label, dataType, choices for list fields - codes are derived), optionally in a new form section (placement.newSectionName).");
  }
}
