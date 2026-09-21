public sealed record GetOptionSetsResponse(IReadOnlyList<OptionSetDto> OptionSets);

public class GetOptionSets : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapGet("/option-sets", async (ISender sender, bool? includeInactive) =>
    {
      var result = await sender.Send(new GetOptionSetsQuery(includeInactive ?? false));
      return Results.Ok(new GetOptionSetsResponse(result.Value!.OptionSets));
    })
      .WithName("GetOptionSets")
      .Produces<GetOptionSetsResponse>(StatusCodes.Status200OK)
      .WithSummary("Get Option Sets")
      .WithDescription("Lists option sets with their values.");
  }
}
