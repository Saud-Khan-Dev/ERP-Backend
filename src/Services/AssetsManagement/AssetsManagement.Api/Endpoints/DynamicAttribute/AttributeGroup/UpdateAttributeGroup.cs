public sealed record UpdateAttributeGroupRequest(AttributeGroupInput Group);
public sealed record UpdateAttributeGroupResponse(bool IsSuccess);

public class UpdateAttributeGroup : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapPut("/attribute-groups/{id}", async (Guid id, UpdateAttributeGroupRequest request, ISender sender) =>
    {
      var result = await sender.Send(new UpdateAttributeGroupCommand(id, request.Group));

      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });

      return Results.Ok(result.Value.Adapt<UpdateAttributeGroupResponse>());
    })
      .WithName("UpdateAttributeGroup")
      .Produces<UpdateAttributeGroupResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Update Attribute Group")
      .WithDescription("Update Attribute Group");
  }
}
