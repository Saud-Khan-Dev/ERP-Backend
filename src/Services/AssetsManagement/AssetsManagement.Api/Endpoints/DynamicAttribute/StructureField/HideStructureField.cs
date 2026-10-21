public sealed record HideStructureFieldRequest(AttributeScope Scope, Guid TargetId, Guid DefinitionId, bool InheritToChildren = true);

/// "Not used here" on a class, type or category's form, and "Use here again".
public class HideStructureField : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapPost("/structure-fields/hide", async (HideStructureFieldRequest request, ISender sender) =>
    {
      var result = await sender.Send(new HideStructureFieldCommand(request.Scope, request.TargetId, request.DefinitionId, request.InheritToChildren));
      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });
      return Results.Ok(result.Value);
    })
      .RequirePermission(PermissionCatalog.AssetAttributes.Edit)

      .WithName("HideStructureField")
      .Produces<HideStructureFieldCommandResult>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Not Used Here")
      .WithDescription("Leaves a field a level above adds (a class field on a type, a type field on a category) off this node's form, and with inheritToChildren off everything below it. Values assets hold stay on them, shown as fields no longer on the form.");

    app.MapDelete("/structure-fields/hide/{assignmentId}", async (Guid assignmentId, ISender sender) =>
    {
      var result = await sender.Send(new UnhideStructureFieldCommand(assignmentId));
      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });
      return Results.Ok(result.Value);
    })
      .RequirePermission(PermissionCatalog.AssetAttributes.Edit)

      .WithName("UnhideStructureField")
      .Produces<HideStructureFieldCommandResult>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Use Here Again")
      .WithDescription("Takes a \"not used here\" mark off, so the field from the level above is on this node's form again.");
  }
}
