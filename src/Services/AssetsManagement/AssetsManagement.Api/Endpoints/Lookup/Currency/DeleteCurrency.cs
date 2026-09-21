public sealed record DeleteCurrencyResponse(bool IsSuccess);

public class DeleteCurrency : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapDelete("/currencies/{code}", async (string code, ISender sender) =>
    {
      var result = await sender.Send(new DeleteCurrencyCommand(code));

      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });

      return Results.Ok(result.Value.Adapt<DeleteCurrencyResponse>());
    })
      .WithName("DeleteCurrency")
      .Produces<DeleteCurrencyResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Delete Currency")
      .WithDescription("Deletes a currency that no financial record references.");
  }
}
