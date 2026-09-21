public sealed record GetAttributeGroupsResponse(IReadOnlyList<AttributeGroupDto> Groups);

public class GetAttributeGroups : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapGet("/attribute-groups", async (ISender sender, bool? includeInactive) =>
    {
      var result = await sender.Send(new GetAttributeGroupsQuery(includeInactive ?? false));
      return Results.Ok(new GetAttributeGroupsResponse(result.Value!.Groups));
    })
      .WithName("GetAttributeGroups")
      .Produces<GetAttributeGroupsResponse>(StatusCodes.Status200OK)
      .WithSummary("Get Attribute Groups")
      .WithDescription("Get Attribute Groups");
  }
}
