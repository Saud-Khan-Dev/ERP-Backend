using System.Text.Json;

public sealed record UpdateAttributeAssignmentRequest(
  Guid? AttributeGroupId,
  string? LabelOverride,
  bool IsRequired,
  bool IsReadonly,
  bool IsSearchable,
  bool IsFilterable,
  bool IsVisibleInList,
  bool InheritToChildren,
  JsonElement? DefaultValue,
  int? DisplayOrder,
  Guid? DependsOnAssignmentId,
  JsonElement? DependsOnValue,
  bool IsActive = true);

public sealed record UpdateAttributeAssignmentResponse(bool IsSuccess);

public class UpdateAttributeAssignment : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapPut("/attribute-assignments/{id}", async (Guid id, UpdateAttributeAssignmentRequest request, ISender sender) =>
    {
      var command = new UpdateAttributeAssignmentCommand(
        id, request.AttributeGroupId, request.LabelOverride, request.IsRequired, request.IsReadonly,
        request.IsSearchable, request.IsFilterable, request.IsVisibleInList, request.InheritToChildren,
        request.DefaultValue, request.DisplayOrder, request.DependsOnAssignmentId, request.DependsOnValue, request.IsActive);

      var result = await sender.Send(command);

      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });

      return Results.Ok(result.Value.Adapt<UpdateAttributeAssignmentResponse>());
    })
      .WithName("UpdateAttributeAssignment")
      .Produces<UpdateAttributeAssignmentResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Update Attribute Assignment")
      .WithDescription("Changes the per-scope overrides of an assignment (the definition and target are fixed).");
  }
}
