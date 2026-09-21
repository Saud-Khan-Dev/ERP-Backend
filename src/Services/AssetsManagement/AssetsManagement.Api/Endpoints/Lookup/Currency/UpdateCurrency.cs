public sealed record UpdateCurrencyRequest(string Name, string? Symbol, short MinorUnits = 2, bool IsActive = true);
public sealed record UpdateCurrencyResponse(bool IsSuccess);

public class UpdateCurrency : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapPut("/currencies/{code}", async (string code, UpdateCurrencyRequest request, ISender sender) =>
    {
      var result = await sender.Send(new UpdateCurrencyCommand(code, request.Name, request.Symbol, request.MinorUnits, request.IsActive));

      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });

      return Results.Ok(result.Value.Adapt<UpdateCurrencyResponse>());
    })
      .WithName("UpdateCurrency")
      .Produces<UpdateCurrencyResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Update Currency")
      .WithDescription("Update Currency");
  }
}
