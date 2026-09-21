public sealed record GetLocationsResponse(IReadOnlyList<LocationDto> Locations);

public class GetLocations : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapGet("/locations", async (ISender sender, Guid? parentLocationId, bool? onlyRoots, string? locationType, bool? includeInactive) =>
    {
      var result = await sender.Send(new GetLocationsQuery(parentLocationId, onlyRoots ?? false, locationType, includeInactive ?? false));
      return Results.Ok(new GetLocationsResponse(result.Value!.Locations));
    })
      .WithName("GetLocations")
      .Produces<GetLocationsResponse>(StatusCodes.Status200OK)
      .WithSummary("Get Locations")
      .WithDescription("Lists locations ordered by path; filter by parent, top level or type (SITE / BUILDING / FLOOR / ROOM).");
  }
}
