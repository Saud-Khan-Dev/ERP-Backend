public sealed record DeleteAttributeGroupResponse(bool IsSuccess);

public class DeleteAttributeGroup : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapDelete("/attribute-groups/{id}", async (Guid id, ISender sender) =>
    {
      var result = await sender.Send(new DeleteAttributeGroupCommand(id));

      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });

      return Results.Ok(result.Value.Adapt<DeleteAttributeGroupResponse>());
    })
      .WithName("DeleteAttributeGroup")
      .Produces<DeleteAttributeGroupResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Delete Attribute Group")
      .WithDescription("Deletes the group; assignments in it become ungrouped.");
  }
}
