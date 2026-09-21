public sealed record CreateAttributeGroupRequest(AttributeGroupInput Group);
public sealed record CreateAttributeGroupResponse(Guid Id);

public class CreateAttributeGroup : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapPost("/attribute-groups", async (CreateAttributeGroupRequest request, ISender sender) =>
    {
      var result = await sender.Send(new CreateAttributeGroupCommand(request.Group));

      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });

      var response = result.Value.Adapt<CreateAttributeGroupResponse>();
      return Results.Created($"/attribute-groups/{response!.Id}", response);
    })
      .WithName("CreateAttributeGroup")
      .Produces<CreateAttributeGroupResponse>(StatusCodes.Status201Created)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Create Attribute Group")
      .WithDescription("UI layout group for dynamic attributes: Hardware, Warranty, Finance ...");
  }
}
