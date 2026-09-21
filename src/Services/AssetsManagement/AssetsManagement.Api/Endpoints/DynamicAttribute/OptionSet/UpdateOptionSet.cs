public sealed record UpdateOptionSetRequest(string Code, string Name, string? Description, bool IsActive = true);
public sealed record UpdateOptionSetResponse(bool IsSuccess);

public class UpdateOptionSet : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapPut("/option-sets/{id}", async (Guid id, UpdateOptionSetRequest request, ISender sender) =>
    {
      var result = await sender.Send(new UpdateOptionSetCommand(id, request.Code, request.Name, request.Description, request.IsActive));

      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });

      return Results.Ok(result.Value.Adapt<UpdateOptionSetResponse>());
    })
      .WithName("UpdateOptionSet")
      .Produces<UpdateOptionSetResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Update Option Set")
      .WithDescription("Updates the option set header; manage values through /option-sets/{id}/values.");
  }
}
