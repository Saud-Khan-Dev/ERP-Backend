public sealed record WithdrawBody(string? Reason);

/// Announcements HR posts in the portal (HR.VIEW to list, HR.CREATE to post, HR.EDIT to change, attach a paper or
/// withdraw); employees read theirs through /me/announcements.
public class AnnouncementEndpoints : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    var group = app.MapGroup("/announcements").WithTags("Announcements");

    group.MapGet("/", async (bool? includeWithdrawn, ISender sender) => (await sender.Send(new GetAnnouncementsQuery(includeWithdrawn ?? false))).ToOk())
      .RequirePermission(PermissionCatalog.Hr.View)
      .WithName("GetAnnouncements")
      .Produces<GetAnnouncementsQueryResult>()
      .WithSummary("Get Announcements")
      .WithDescription("Newest first, with how many of the people each is for have opened it.");

    group.MapPost("/", async (AnnouncementInput input, ISender sender) => (await sender.Send(new PostAnnouncementCommand(input))).ToOk())
      .RequirePermission(PermissionCatalog.Hr.Create)
      .WithName("PostAnnouncement")
      .Produces<CreatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Post Announcement")
      .WithDescription("orgUnitId: the unit it is for, with the units under it (null = everyone). It shows from showFrom to showUntil (open-ended when null).");

    group.MapPut("/{id:guid}", async (Guid id, AnnouncementInput input, ISender sender) => (await sender.Send(new EditAnnouncementCommand(id, input))).ToOk())
      .RequirePermission(PermissionCatalog.Hr.Edit)
      .WithName("EditAnnouncement")
      .Produces<UpdatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Edit Announcement");

    group.MapPost("/{id:guid}/withdraw", async (Guid id, WithdrawBody? body, ISender sender) => (await sender.Send(new WithdrawAnnouncementCommand(id, body?.Reason))).ToOk())
      .RequirePermission(PermissionCatalog.Hr.Edit)
      .WithName("WithdrawAnnouncement")
      .Produces<UpdatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Withdraw Announcement")
      .WithDescription("Taken down at once; kept with who, when and why (the reason is required).");

    group.MapPost("/{id:guid}/file", async (Guid id, [Microsoft.AspNetCore.Mvc.FromForm] FileUploadForm form, ISender sender) =>
    {
      await using var content = Uploads.Open(form.File, out var file);
      return (await sender.Send(new AttachAnnouncementFileCommand(id, file))).ToOk();
    })
      .DisableAntiforgery()
      .Accepts<FileUploadForm>("multipart/form-data")
      .RequirePermission(PermissionCatalog.Hr.Edit)
      .WithName("AttachAnnouncementFile")
      .Produces<UpdatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Attach a Paper")
      .WithDescription("The notification or circular itself; replaces the one attached.");

    group.MapGet("/{id:guid}/file", async (Guid id, bool? download, HttpResponse response, ISender sender) =>
        (await sender.Send(new GetAnnouncementFileQuery(id, null, download ?? true))).ToFile(response))
      .RequirePermission(PermissionCatalog.Hr.View)
      .WithName("GetAnnouncementFile")
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("The Paper of an Announcement");

    // ---- the portal ----

    var me = app.MapGroup("/me/announcements").WithTags("Self Service").RequireAuthorization().AllOffices();
    static async Task<Guid> Self(CurrentEmployee current, CancellationToken cancellationToken) => (await current.IdAsync(cancellationToken)).Value;

    me.MapGet("/", async (CurrentEmployee current, ISender sender, CancellationToken ct) => (await sender.Send(new GetMyAnnouncementsQuery(await Self(current, ct)))).ToOk())
      .WithName("GetMyAnnouncements")
      .Produces<GetAnnouncementsQueryResult>()
      .WithSummary("My Announcements")
      .WithDescription("Running today, for everyone or for my unit (or a unit above it); pinned first, then newest; isRead = I opened it.");

    me.MapPost("/{id:guid}/read", async (Guid id, CurrentEmployee current, ISender sender, CancellationToken ct) =>
        (await sender.Send(new MarkAnnouncementReadCommand(id, await Self(current, ct)))).ToOk())
      .WithName("ReadMyAnnouncement")
      .Produces<UpdatedResult>()
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("I Opened It");

    me.MapGet("/{id:guid}/file", async (Guid id, bool? download, CurrentEmployee current, HttpResponse response, ISender sender, CancellationToken ct) =>
        (await sender.Send(new GetAnnouncementFileQuery(id, await Self(current, ct), download ?? true))).ToFile(response))
      .WithName("GetMyAnnouncementFile")
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("The Paper of an Announcement for Me");
  }
}
