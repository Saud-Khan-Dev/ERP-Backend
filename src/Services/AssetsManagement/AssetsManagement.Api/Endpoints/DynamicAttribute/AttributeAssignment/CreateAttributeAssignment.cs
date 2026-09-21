public sealed record CreateAttributeAssignmentRequest(AttributeAssignmentInput Assignment);
public sealed record CreateAttributeAssignmentResponse(Guid Id);

public class CreateAttributeAssignment : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapPost("/attribute-assignments", async (CreateAttributeAssignmentRequest request, ISender sender) =>
    {
      var result = await sender.Send(new CreateAttributeAssignmentCommand(request.Assignment));

      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });

      var response = result.Value.Adapt<CreateAttributeAssignmentResponse>();
      return Results.Created($"/attribute-assignments/{response!.Id}", response);
    })
      .WithName("CreateAttributeAssignment")
      .Produces<CreateAttributeAssignmentResponse>(StatusCodes.Status201Created)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Create Attribute Assignment")
      .WithDescription("Attaches an attribute to ONE scope (asset class / asset type / category / asset) with per-scope overrides (required, readonly, default, filterable ...).");
  }
}
