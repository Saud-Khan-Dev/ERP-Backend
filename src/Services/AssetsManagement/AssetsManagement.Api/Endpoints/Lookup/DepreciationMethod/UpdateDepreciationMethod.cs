public sealed record UpdateDepreciationMethodRequest(DepreciationMethodInput Method);
public sealed record UpdateDepreciationMethodResponse(bool IsSuccess);

public class UpdateDepreciationMethod : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapPut("/depreciation-methods/{id}", async (Guid id, UpdateDepreciationMethodRequest request, ISender sender) =>
    {
      var result = await sender.Send(new UpdateDepreciationMethodCommand(id, request.Method));

      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });

      return Results.Ok(result.Value.Adapt<UpdateDepreciationMethodResponse>());
    })
      .WithName("UpdateDepreciationMethod")
      .Produces<UpdateDepreciationMethodResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Update Depreciation Method")
      .WithDescription("Update Depreciation Method");
  }
}
