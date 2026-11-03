/// The staff directory: who people are and where they sit, for pickers and labels in the other modules (an asset's
/// custodian, the HR record of a sign-in account, the officer who signs a tax notice). Any signed-in person may read
/// it, so it carries nothing a notice board would not - no pay, CNIC or contacts.
public class DirectoryEndpoints : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    // the directory is the whole staff list for every module: never limited by the reader's office scope
    var directory = app.MapGroup("/directory").WithTags("Directory").AllOffices();

    directory.MapGet("/employees", async (int? pageIndex, int? pageSize, string? search, string? ids, bool? includeLeft, ISender sender) =>
        (await sender.Send(new GetDirectoryEmployeesQuery(QueryParsing.Page(pageIndex, pageSize), search, QueryParsing.ParseGuids(ids, "ids"), includeLeft ?? false))).ToOk())
      .RequireSignedIn()
      .WithName("GetDirectoryEmployees")
      .Produces<GetDirectoryEmployeesQueryResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Staff directory")
      .WithDescription("People with their number, designation and unit today. search matches part of the number or name; ids=a,b,c names given people "
        + "(those who left included). People who left service are left out unless includeLeft=true.");

    // contact details stay out of the directory; only the person opening their sign-in account reads the email
    directory.MapGet("/employees/{id:guid}/email", async (Guid id, ISender sender) => (await sender.Send(new GetDirectoryEmailQuery(id))).ToOk())
      .RequireAnyPermission(PermissionCatalog.Users.Create, PermissionCatalog.Users.Edit)
      .WithName("GetDirectoryEmail")
      .Produces<DirectoryEmailDto>()
      .WithSummary("A person's email, for their account")
      .WithDescription("The email on the person's HR record (the primary one first), or null - for registering or editing their sign-in account. IAM_USERS.CREATE or EDIT.");

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

    directory.MapGet("/offices", async (ISender sender) => (await sender.Send(new GetDirectoryOfficesQuery())).ToOk())
      .RequireSignedIn()
      .WithName("GetDirectoryOffices")
      .Produces<GetDirectoryOfficesQueryResult>()
      .WithSummary("Offices for data scope")
      .WithDescription("The active units of the structure today with their parent and type - what Administration offers when it limits a person to "
        + "some offices (a person limited to an office works on it and every unit below it).");

    // gazetted holidays are public facts: a scheduled job (Taxation's daily job) reads them without a token
    directory.MapGet("/holidays", async (DateOnly? from, DateOnly? to, IClock clock, ISender sender) =>
      {
        var start = from ?? new DateOnly(clock.Today.Year, 1, 1);
        return (await sender.Send(new GetPublicHolidaysQuery(start, to ?? new DateOnly(start.Year, 12, 31)))).ToOk();
      })
      .AllowAnonymous()
      .WithName("GetPublicHolidays")
      .Produces<GetPublicHolidaysQueryResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Gazetted holidays (public)")
      .WithDescription("Organization-wide holidays that are not optional, between from and to (default: this year; at most three years). "
        + "No sign-in needed: { holidays: [{ date, name, holidayType }] }.");
  }
}
