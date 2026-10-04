/// Data for the Property reports (the client produces the printable page and the CSV from it).
public class PropertyReportEndpoints : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    var reports = app.MapGroup("/property-reports").WithTags("Reports");

    reports.MapGet("/register", async (ISender sender, [AsParameters] PropertyFilterParameters filter, Guid[]? ids) =>
        (await sender.Send(new GetPropertyRegisterReportQuery(filter.ToQuery(new PaginationRequest(0, 1)), ids))).ToOk())
      .RequirePermission(PermissionCatalog.Property.View)
      .WithName("GetPropertyRegisterReport")
      .Produces<GetPropertyRegisterReportQueryResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Property Register Report")
      .WithDescription("Every property matching the register's filters (the same parameters as GET /properties, no paging, at most 5000), or the properties "
        + "given as ids=…&ids=… (at most 150), with full area in sq ft, current owners and custom-field values keyed by field code. totals: count and total area.");

    reports.MapGet("/ownership", async (
        ISender sender, DateOnly? asOf, Guid? townId, Guid? ownerTypeId, Guid? propertyId, Guid? ownerId, string? search,
        bool? includeDisputed, bool? includeInactive) =>
        (await sender.Send(new GetOwnershipReportQuery(asOf, townId, ownerTypeId, propertyId, ownerId, search,
          includeDisputed ?? false, includeInactive ?? false))).ToOk())
      .RequirePermission(PermissionCatalog.Property.View)
      .WithName("GetOwnershipReport")
      .Produces<GetOwnershipReportQueryResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Ownership Report")
      .WithDescription("Who held which share of which property on asOf (default today): shares whose period covers that date, active or since ended. "
        + "Disputed shares only with includeDisputed=true; properties taken off the register only with includeInactive=true. At most 5000 rows.");
  }
}
