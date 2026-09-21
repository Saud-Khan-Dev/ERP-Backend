public sealed record GetDepreciationMethodsResponse(IReadOnlyList<DepreciationMethodDto> Methods);

public class GetDepreciationMethods : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapGet("/depreciation-methods", async (ISender sender, bool? includeInactive) =>
    {
      var result = await sender.Send(new GetDepreciationMethodsQuery(includeInactive ?? false));
      return Results.Ok(new GetDepreciationMethodsResponse(result.Value!.Methods));
    })
      .WithName("GetDepreciationMethods")
      .Produces<GetDepreciationMethodsResponse>(StatusCodes.Status200OK)
      .WithSummary("Get Depreciation Method list")
      .WithDescription("Get Depreciation Method list");
  }
}
