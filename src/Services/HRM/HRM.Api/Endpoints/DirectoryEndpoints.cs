/// The staff directory: who people are and where they sit, for pickers and labels in the other modules (an asset's
/// custodian, the HR record of a sign-in account, the officer who signs a tax notice). Any signed-in person may read
/// it, so it carries nothing a notice board would not - no pay, CNIC or contacts.
public class DirectoryEndpoints : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    var directory = app.MapGroup("/directory").WithTags("Directory");

    directory.MapGet("/employees", async (int? pageIndex, int? pageSize, string? search, string? ids, bool? includeLeft, ISender sender) =>
        (await sender.Send(new GetDirectoryEmployeesQuery(QueryParsing.Page(pageIndex, pageSize), search, QueryParsing.ParseGuids(ids, "ids"), includeLeft ?? false))).ToOk())
      .RequireSignedIn()
      .WithName("GetDirectoryEmployees")
      .Produces<GetDirectoryEmployeesQueryResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Staff directory")
      .WithDescription("People with their number, designation and unit today. search matches part of the number or name; ids=a,b,c names given people "
        + "(those who left included). People who left service are left out unless includeLeft=true.");

    directory.MapGet("/org-units", async (ISender sender) => (await sender.Send(new GetDirectoryOrgUnitsQuery())).ToOk())
      .RequireSignedIn()
      .WithName("GetDirectoryOrgUnits")
      .Produces<GetDirectoryOrgUnitsQueryResult>()
      .WithSummary("Units of the organization")
      .WithDescription("Every unit on the structure today, with its parent - for department pickers in other modules.");

    directory.MapGet("/posts/{postId:guid}/holder", async (Guid postId, DateOnly? on, ISender sender) =>
        (await sender.Send(new GetPostHolderQuery(postId, on))).ToOk())
      .RequireSignedIn()
      .WithName("GetPostHolder")
      .Produces<DirectoryPostHolderDto>()
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Who holds a post")
      .WithDescription("The post's regular holder on a date (today by default); null when vacant.");
  }
}
