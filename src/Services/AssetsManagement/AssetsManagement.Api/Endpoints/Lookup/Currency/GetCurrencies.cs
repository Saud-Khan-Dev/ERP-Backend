public sealed record GetCurrenciesResponse(IReadOnlyList<CurrencyLookupDto> Currencies);

public class GetCurrencies : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapGet("/currencies", async (ISender sender, bool? includeInactive) =>
    {
      var result = await sender.Send(new GetCurrenciesQuery(includeInactive ?? false));
      return Results.Ok(new GetCurrenciesResponse(result.Value!.Currencies));
    })
      .WithName("GetCurrencies")
      .Produces<GetCurrenciesResponse>(StatusCodes.Status200OK)
      .WithSummary("Get Currencies")
      .WithDescription("Get Currencies");
  }
}
