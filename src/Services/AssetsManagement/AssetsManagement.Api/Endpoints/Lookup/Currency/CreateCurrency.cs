public sealed record CreateCurrencyRequest(CurrencyInput Currency);
public sealed record CreateCurrencyResponse(string Code);

public class CreateCurrency : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapPost("/currencies", async (CreateCurrencyRequest request, ISender sender) =>
    {
      var result = await sender.Send(new CreateCurrencyCommand(request.Currency));

      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });

      var response = result.Value.Adapt<CreateCurrencyResponse>();
      return Results.Created($"/currencies/{response!.Code}", response);
    })
      .WithName("CreateCurrency")
      .Produces<CreateCurrencyResponse>(StatusCodes.Status201Created)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Create Currency")
      .WithDescription("ISO 4217 currency with minor units (rounding rule).");
  }
}
