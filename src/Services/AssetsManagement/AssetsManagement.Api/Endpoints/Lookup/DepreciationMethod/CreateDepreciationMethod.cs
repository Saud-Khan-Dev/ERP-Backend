public sealed record CreateDepreciationMethodRequest(DepreciationMethodInput Method);
public sealed record CreateDepreciationMethodResponse(Guid Id);

public class CreateDepreciationMethod : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapPost("/depreciation-methods", async (CreateDepreciationMethodRequest request, ISender sender) =>
    {
      var result = await sender.Send(new CreateDepreciationMethodCommand(request.Method));

      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });

      var response = result.Value.Adapt<CreateDepreciationMethodResponse>();
      return Results.Created($"/depreciation-methods/{response!.Id}", response);
    })
      .WithName("CreateDepreciationMethod")
      .Produces<CreateDepreciationMethodResponse>(StatusCodes.Status201Created)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Create Depreciation Method")
      .WithDescription("Create Depreciation Method");
  }
}
