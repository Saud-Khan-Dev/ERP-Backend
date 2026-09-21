public sealed record UpdateOptionSetValueRequest(OptionSetValueInput Value);
public sealed record UpdateOptionSetValueResponse(bool IsSuccess);

public class UpdateOptionSetValue : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapPut("/option-sets/{id}/values/{valueId}", async (Guid id, Guid valueId, UpdateOptionSetValueRequest request, ISender sender) =>
    {
      var result = await sender.Send(new UpdateOptionSetValueCommand(id, valueId, request.Value));

      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });

      return Results.Ok(result.Value.Adapt<UpdateOptionSetValueResponse>());
    })
      .WithName("UpdateOptionSetValue")
      .Produces<UpdateOptionSetValueResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Update Option Set Value")
      .WithDescription("Updates label / order / status of a value; the stored code is immutable once assets use it.");
  }
}
