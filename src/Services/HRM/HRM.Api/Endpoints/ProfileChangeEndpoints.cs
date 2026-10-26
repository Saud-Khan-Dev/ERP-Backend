public sealed class PhotoChangeForm
{
  public IFormFile? File { get; set; }
  public string? Note { get; set; }
}

/// Profile changes employees ask for through the portal (/me/profile-changes), decided here by HR. Their contents are
/// personal facts, so reading them needs HR_SENSITIVE.VIEW as well, and deciding them HR.APPROVE (never one's own).
public class ProfileChangeEndpoints : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    var changes = app.MapGroup("/profile-changes").WithTags("Profile Changes");

    changes.MapGet("/", async (int? pageIndex, int? pageSize, Guid? employeeId, string? status, string? search, ISender sender) =>
        (await sender.Send(new GetProfileChangesQuery(QueryParsing.Page(pageIndex, pageSize), employeeId,
          QueryParsing.ParseEnum<ProfileChangeStatus>(status, "status"), search))).ToOk())
      .RequirePermission(PermissionCatalog.Hr.View)
      .RequirePermission(PermissionCatalog.HrSensitive.View)
      .WithName("GetProfileChanges")
      .Produces<GetProfileChangesQueryResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Get Profile Changes")
      .WithDescription("status = pending (oldest first) | approved | rejected | withdrawn (newest first); search = what changes, employee number or name.");

    changes.MapGet("/{id:guid}/photo", async (Guid id, HttpResponse response, ISender sender) =>
        (await sender.Send(new GetProfileChangePhotoQuery(id))).ToFile(response))
      .RequirePermission(PermissionCatalog.Hr.View)
      .WithName("GetProfileChangePhoto")
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Proposed Photo");

    foreach (var (approve, route, summary) in new[] { (true, "approve", "Approve Profile Change"), (false, "reject", "Reject Profile Change") })
    {
      changes.MapPost($"/{{id:guid}}/{route}", async (Guid id, DecideRequestBody? r, ISender sender) =>
          (await sender.Send(new DecideProfileChangeCommand(id, approve, r?.Remarks))).ToOk())
        .RequirePermission(PermissionCatalog.Hr.Approve)
        .RequirePermission(PermissionCatalog.HrSensitive.View)
        .WithName(approve ? "ApproveProfileChange" : "RejectProfileChange")
        .Produces<UpdatedResult>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary(summary)
        .WithDescription(approve
          ? "Makes the change on the record (through the same rules as HR's own form) together with the decision; a change the record no longer allows is refused and stays waiting."
          : "Remarks (the reason, so the employee can put it right) are required.");
    }
  }
}
