public sealed record GetAttributeAssignmentsResponse(IReadOnlyList<AttributeAssignmentDto> Assignments);

public class GetAttributeAssignments : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapGet("/attribute-assignments", async (
      ISender sender,
      AttributeScope? scope,
      Guid? targetId,
      Guid? attributeDefinitionId,
      bool? includeInactive) =>
    {
      var result = await sender.Send(new GetAttributeAssignmentsQuery(scope, targetId, attributeDefinitionId, includeInactive ?? false));
      return Results.Ok(new GetAttributeAssignmentsResponse(result.Value!.Assignments));
    })
      .WithName("GetAttributeAssignments")
      .Produces<GetAttributeAssignmentsResponse>(StatusCodes.Status200OK)
      .WithSummary("Get Attribute Assignments")
      .WithDescription("Assignments attached directly to a scope target (scope + targetId), or all uses of one definition. No inheritance — see /attribute-schema.");
  }
}
