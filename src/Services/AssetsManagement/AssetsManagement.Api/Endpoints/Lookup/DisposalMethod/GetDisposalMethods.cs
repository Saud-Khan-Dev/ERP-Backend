public sealed record GetDisposalMethodsResponse(IReadOnlyList<DisposalMethodDto> Methods);

public class GetDisposalMethods : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapGet("/disposal-methods", async (ISender sender, bool? includeInactive) =>
    {
      var result = await sender.Send(new GetDisposalMethodsQuery(includeInactive ?? false));
      return Results.Ok(new GetDisposalMethodsResponse(result.Value!.Methods));
    })
      .WithName("GetDisposalMethods")
      .Produces<GetDisposalMethodsResponse>(StatusCodes.Status200OK)
      .WithSummary("Get Disposal Method list")
      .WithDescription("Get Disposal Method list");
  }
}
