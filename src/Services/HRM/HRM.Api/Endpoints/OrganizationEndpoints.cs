public sealed record CreateOrgUnitRequest(string Code, OrgUnitDetailsInput Details, DateOnly EffectiveFrom);
public sealed record OrgUnitChangeRequest(OrgUnitDetailsInput Details, DateOnly EffectiveFrom);
public sealed record EffectiveDateRequest(DateOnly EffectiveFrom);
public sealed record UpdatePayScaleRequest(string? IncrementRule, string? VersionLabel, string? NotificationRef);
public sealed record ReplacePayScaleStagesRequest(decimal? AnnualIncrement, IReadOnlyList<PayScaleStageInput>? Stages);
public sealed record CreatePostRequest(string? PostCode, PostDetailsInput Details, DateOnly EffectiveFrom);
public sealed record PostChangeRequest(PostDetailsInput Details, DateOnly EffectiveFrom);
public sealed record PostLifecycleRequest(DateOnly EffectiveFrom, string? NotificationRef);

/// The establishment: org units (effective-dated tree), pay scales (notified BPS scales and stages) and sanctioned
/// posts (effective-dated, seats filled derived from who holds them).
public class OrganizationEndpoints : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    // ---- org units ----
    var units = app.MapGroup("/org-units").WithTags("Organization");

    units.MapGet("/", async (DateOnly? asOf, bool? includeInactive, string? search, Guid? parentUnitId, Guid? unitTypeId, ISender sender) =>
        (await sender.Send(new GetOrgUnitsQuery(asOf, includeInactive ?? false, search, parentUnitId, unitTypeId))).ToOk())
      .RequireAnyPermission(HrmAuthorization.AnyHrmReader)
      .WithName("GetOrgUnits")
      .Produces<GetOrgUnitsQueryResult>()
      .WithSummary("Get Org Units")
      .WithDescription("Every unit as it is on asOf (default today), with depth and the sanctioned / filled seats of its posts. includeInactive also lists closed units.");

    units.MapGet("/tree", async (DateOnly? asOf, ISender sender) => (await sender.Send(new GetOrgUnitTreeQuery(asOf))).ToOk())
      .RequireAnyPermission(HrmAuthorization.AnyHrmReader)
      .WithName("GetOrgUnitTree")
      .Produces<GetOrgUnitTreeQueryResult>()
      .WithSummary("Get Org Chart")
      .WithDescription("The active structure on asOf as a tree; seats roll up from sub-units.");

    units.MapGet("/{id:guid}", async (Guid id, ISender sender) => (await sender.Send(new GetOrgUnitQuery(id))).ToOk())
      .RequireAnyPermission(HrmAuthorization.AnyHrmReader)
      .WithName("GetOrgUnit")
      .Produces<GetOrgUnitQueryResult>()
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Get Org Unit")
      .WithDescription("The unit with every version, newest first.");

    units.MapPost("/", async (CreateOrgUnitRequest request, ISender sender) =>
        (await sender.Send(new CreateOrgUnitCommand(request.Code, request.Details, request.EffectiveFrom))).ToCreated(r => $"/org-units/{r.Id}"))
      .RequirePermission(PermissionCatalog.HrSetup.Create)
      .WithName("CreateOrgUnit")
      .Produces<CreatedResult>(StatusCodes.Status201Created)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Create Org Unit")
      .WithDescription("The code is permanent. The parent must be active on effectiveFrom; the head post is set once the unit has posts.");

    units.MapPost("/{id:guid}/versions", async (Guid id, OrgUnitChangeRequest request, ISender sender) =>
        (await sender.Send(new RestructureOrgUnitCommand(id, request.Details, request.EffectiveFrom))).ToOk())
      .RequirePermission(PermissionCatalog.HrSetup.Edit)
      .WithName("RestructureOrgUnit")
      .Produces<CreatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Restructure Org Unit")
      .WithDescription("Rename, move under another parent, change type, location or head post from a date. The current version closes the day before; history is kept.");

    units.MapPut("/{id:guid}/current", async (Guid id, OrgUnitDetailsInput details, ISender sender) =>
        (await sender.Send(new CorrectOrgUnitCommand(id, details))).ToOk())
      .RequirePermission(PermissionCatalog.HrSetup.Edit)
      .WithName("CorrectOrgUnit")
      .Produces<UpdatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Correct Org Unit")
      .WithDescription("Fixes a mistake in the latest version (e.g. a typo) without starting a new period.");

    units.MapPost("/{id:guid}/deactivate", async (Guid id, EffectiveDateRequest request, ISender sender) =>
        (await sender.Send(new SetOrgUnitActivationCommand(id, false, request.EffectiveFrom))).ToOk())
      .RequirePermission(PermissionCatalog.HrSetup.Delete)
      .WithName("DeactivateOrgUnit")
      .Produces<CreatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Close Org Unit")
      .WithDescription("The unit stops from a date. It must have no active sub-units and no posts that day.");

    units.MapPost("/{id:guid}/reactivate", async (Guid id, EffectiveDateRequest request, ISender sender) =>
        (await sender.Send(new SetOrgUnitActivationCommand(id, true, request.EffectiveFrom))).ToOk())
      .RequirePermission(PermissionCatalog.HrSetup.Edit)
      .WithName("ReactivateOrgUnit")
      .Produces<CreatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Reopen Org Unit");

    // ---- pay scales ----
    var scales = app.MapGroup("/pay-scales").WithTags("Pay Scales");

    scales.MapGet("/", async (Guid? gradeId, DateOnly? asOf, bool? includeInactive, ISender sender) =>
        (await sender.Send(new GetPayScalesQuery(gradeId, asOf, includeInactive ?? false))).ToOk())
      .RequireAnyPermission(HrmAuthorization.AnyHrmReader)
      .WithName("GetPayScales")
      .Produces<GetPayScalesQueryResult>()
      .WithSummary("Get Pay Scales")
      .WithDescription("Notified BPS scales with their stages; asOf limits to the scales in force that day. inUse = employees are paid on its stages.");

    scales.MapGet("/{id:guid}", async (Guid id, ISender sender) => (await sender.Send(new GetPayScaleQuery(id))).ToOk())
      .RequireAnyPermission(HrmAuthorization.AnyHrmReader)
      .WithName("GetPayScale")
      .Produces<GetPayScaleQueryResult>()
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Get Pay Scale");

    scales.MapPost("/", async (CreatePayScalesCommand command, ISender sender) => (await sender.Send(command)).ToOk())
      .RequirePermission(PermissionCatalog.HrSetup.Create)
      .WithName("CreatePayScales")
      .Produces<CreatePayScalesCommandResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Notify Pay Scales")
      .WithDescription("One notification for one or many grades (e.g. Revised Pay Scales 2025 for BPS 1-22), in one transaction. Each grade gives the annual increment (stages 0..n laid out from the minimum) or its stages. The scale in force closes the day before.");

    scales.MapPut("/{id:guid}", async (Guid id, UpdatePayScaleRequest request, ISender sender) =>
        (await sender.Send(new UpdatePayScaleCommand(id, request.IncrementRule, request.VersionLabel, request.NotificationRef))).ToOk())
      .RequirePermission(PermissionCatalog.HrSetup.Edit)
      .WithName("UpdatePayScale")
      .Produces<UpdatedResult>()
      .WithSummary("Update Pay Scale Description");

    scales.MapPut("/{id:guid}/stages", async (Guid id, ReplacePayScaleStagesRequest request, ISender sender) =>
        (await sender.Send(new ReplacePayScaleStagesCommand(id, request.AnnualIncrement, request.Stages))).ToOk())
      .RequirePermission(PermissionCatalog.HrSetup.Edit)
      .WithName("ReplacePayScaleStages")
      .Produces<UpdatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Replace Pay Scale Stages")
      .WithDescription("Only while nobody is paid on the scale.");

    scales.MapPost("/{id:guid}/deactivate", async (Guid id, ISender sender) =>
        (await sender.Send(new SetPayScaleStatusCommand(id, RecordStatus.Inactive))).ToOk())
      .RequirePermission(PermissionCatalog.HrSetup.Delete)
      .WithName("DeactivatePayScale")
      .Produces<UpdatedResult>()
      .WithSummary("Withdraw Pay Scale")
      .WithDescription("A withdrawn scale is no longer used to place anyone; existing pay records keep their stage.");

    scales.MapPost("/{id:guid}/activate", async (Guid id, ISender sender) =>
        (await sender.Send(new SetPayScaleStatusCommand(id, RecordStatus.Active))).ToOk())
      .RequirePermission(PermissionCatalog.HrSetup.Edit)
      .WithName("ActivatePayScale")
      .Produces<UpdatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Restore Pay Scale");

    // ---- posts ----
    var posts = app.MapGroup("/posts").WithTags("Posts");

    posts.MapGet("/", async (int? pageIndex, int? pageSize, DateOnly? asOf, Guid? orgUnitId, bool? includeSubUnits, Guid? designationId, Guid? gradeId,
        string? status, string? employmentType, string? search, string? sortBy, string? sortDir, ISender sender) =>
        (await sender.Send(new GetPostsQuery(QueryParsing.Page(pageIndex, pageSize), asOf, orgUnitId, includeSubUnits ?? true, designationId, gradeId,
          QueryParsing.ParseEnum<PositionStatus>(status, "status"), QueryParsing.ParseEnum<EmploymentType>(employmentType, "employmentType"),
          search, QueryParsing.ParseEnum<PostSort>(sortBy, "sortBy") ?? PostSort.Code, QueryParsing.ParseDescending(sortDir) ?? false))).ToOk())
      .RequireAnyPermission(HrmAuthorization.AnyHrmReader)
      .WithName("GetPosts")
      .Produces<GetPostsQueryResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Get Posts")
      .WithDescription("Sanctioned posts as they are on asOf (default today) with who holds them. status = vacant | partially_filled | filled | frozen | abolished; orgUnitId includes sub-units unless includeSubUnits=false; search matches the post code; sortBy = code | bps | vacancy.");

    posts.MapGet("/next-code", async (ISender sender) => (await sender.Send(new GetNextPostCodeQuery())).ToOk())
      .RequirePermission(PermissionCatalog.HrSetup.Create)
      .WithName("GetNextPostCode")
      .Produces<GetNextPostCodeQueryResult>()
      .WithSummary("Preview Next Post Code")
      .WithDescription("The code the next post would get when none is typed. A preview only: nothing is reserved.");

    posts.MapGet("/{id:guid}", async (Guid id, ISender sender) => (await sender.Send(new GetPostQuery(id))).ToOk())
      .RequireAnyPermission(HrmAuthorization.AnyHrmReader)
      .WithName("GetPost")
      .Produces<GetPostQueryResult>()
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Get Post")
      .WithDescription("The post with every version and every assignment (who held it, from when).");

    posts.MapPost("/", async (CreatePostRequest request, ISender sender) =>
        (await sender.Send(new CreatePostCommand(request.PostCode, request.Details, request.EffectiveFrom))).ToCreated(r => $"/posts/{r.Id}"))
      .RequirePermission(PermissionCatalog.HrSetup.Create)
      .WithName("CreatePost")
      .Produces<CreatePostCommandResult>(StatusCodes.Status201Created)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Sanction Post")
      .WithDescription("A new sanctioned post (sanctionedCount seats, e.g. 10 for a pool of drivers). Leave postCode empty to have one issued.");

    posts.MapPost("/{id:guid}/versions", async (Guid id, PostChangeRequest request, ISender sender) =>
        (await sender.Send(new RevisePostCommand(id, request.Details, request.EffectiveFrom))).ToOk())
      .RequirePermission(PermissionCatalog.HrSetup.Edit)
      .WithName("RevisePost")
      .Produces<CreatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Revise Post")
      .WithDescription("Upgrade / downgrade, move to another unit, change seats or the reporting post from a date. Seats cannot drop below the holders that day.");

    posts.MapPut("/{id:guid}/current", async (Guid id, PostDetailsInput details, ISender sender) =>
        (await sender.Send(new CorrectPostCommand(id, details))).ToOk())
      .RequirePermission(PermissionCatalog.HrSetup.Edit)
      .WithName("CorrectPost")
      .Produces<UpdatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Correct Post")
      .WithDescription("Fixes a mistake in the latest version without starting a new period.");

    foreach (var (action, route, permission, summary, description) in new[]
    {
      (PostLifecycleAction.Freeze, "freeze", PermissionCatalog.HrSetup.Edit, "Freeze Post", "No new regular appointment from the date; current holders stay."),
      (PostLifecycleAction.Unfreeze, "unfreeze", PermissionCatalog.HrSetup.Edit, "Unfreeze Post", "The post takes regular appointments again from the date."),
      (PostLifecycleAction.Abolish, "abolish", PermissionCatalog.HrSetup.Delete, "Abolish Post", "The post ceases from the date. It must have no regular holder that day or later, and head no unit.")
    })
    {
      posts.MapPost($"/{{id:guid}}/{route}", async (Guid id, PostLifecycleRequest request, ISender sender) =>
          (await sender.Send(new ChangePostLifecycleCommand(id, action, request.EffectiveFrom, request.NotificationRef))).ToOk())
        .RequirePermission(permission)
        .WithName($"{action}Post")
        .Produces<CreatedResult>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .WithSummary(summary)
        .WithDescription(description);
    }
  }
}
