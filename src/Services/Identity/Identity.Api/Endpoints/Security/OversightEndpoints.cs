/// Administration's oversight: the Overview's figures, the access report behind the fixed access reports, every session
/// open now, the oversight settings, the access review and the permission pairs that should not meet.
public class OversightEndpoints : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapGet("/oversight", async (ISender sender) => Results.Ok(new OversightResponse((await sender.Send(new GetOversightQuery())).Value!)))
      .RequirePermission(PermissionCatalog.Users.View)
      .WithName("GetOversight")
      .Produces<OversightResponse>(StatusCodes.Status200OK)
      .WithSummary("What An Owner Keeps An Eye On")
      .WithDescription("Super Administrators, dormant accounts, exceptions in force, duty rules switched off, accounts holding conflicting duties, the access review, open sessions, failed sign-ins, locked accounts and the last 30 days' acts that change who can do what.");

    app.MapGet("/access-report", async (ISender sender) => Results.Ok(new AccessReportResponse((await sender.Send(new GetAccessReportQuery())).Value!)))
      .RequirePermission(PermissionCatalog.Users.View)
      .WithName("GetAccessReport")
      .Produces<AccessReportResponse>(StatusCodes.Status200OK)
      .WithSummary("Every Account's Access, And Why")
      .WithDescription("Each account (deleted ones left out): roles, where it works, every permission it holds with where it comes from (a role, an exception), exceptions that deny, and the duties it holds together that should be kept apart.");

    app.MapGet("/permissions/conflicts", () => Results.Ok(new ConflictsResponse(
        PermissionCatalog.Conflicts.Select(c => new PermissionConflictDto(c.First, c.Second, c.Reason)).ToList())))
      .RequireAuthorization()
      .WithName("GetPermissionConflicts")
      .Produces<ConflictsResponse>(StatusCodes.Status200OK)
      .WithSummary("Permissions That Should Not Meet")
      .WithDescription("The pairs one person should not hold together, with the reason - the role editor and Change roles warn and ask why.");

    app.MapGet("/sessions", async (ISender sender) => Results.Ok(new OpenSessionsResponse((await sender.Send(new GetOpenSessionsQuery())).Value!)))
      .RequirePermission(PermissionCatalog.Security.View)
      .WithName("GetOpenSessions")
      .Produces<OpenSessionsResponse>(StatusCodes.Status200OK)
      .WithSummary("Everyone Signed In Now")
      .WithDescription("Every open session (at most 500, newest first): who, from where, signed in when, last renewed.");

    app.MapPost("/sessions/{id}/revoke", async (Guid id, ISender sender) =>
    {
      var result = await sender.Send(new RevokeSessionCommand(id));
      return result.IsSuccess ? Results.NoContent() : Results.BadRequest(new { result.Message });
    })
      .RequirePermission(PermissionCatalog.Security.Revoke)
      .WithName("RevokeSession")
      .Produces(StatusCodes.Status204NoContent)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Sign One Session Out")
      .WithDescription("Ends one device's sign-in at once (its tokens stop working). Not on an account that can do more than you.");

    app.MapPut("/security-settings/oversight", async (OversightSettingsDto request, ISender sender) =>
    {
      var result = await sender.Send(new UpdateOversightSettingsCommand(request));
      return Results.Ok(new OversightSettingsResponse(result.Value!));
    })
      .RequirePermission(PermissionCatalog.Security.Edit)
      .WithName("UpdateOversightSettings")
      .Produces<OversightSettingsResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Oversight Settings")
      .WithDescription("Dormant after N days (30-365), switch dormant accounts off automatically, access review every N months (1-24), the longest sign-in in days (1-90).");

    app.MapGet("/access-reviews", async (ISender sender) => Results.Ok(new AccessReviewsResponse((await sender.Send(new GetAccessReviewsQuery())).Value!)))
      .RequirePermission(PermissionCatalog.Users.View)
      .WithName("GetAccessReviews")
      .Produces<AccessReviewsResponse>(StatusCodes.Status200OK)
      .WithSummary("Access Reviews")
      .WithDescription("The review in progress with its decisions, the last reviews signed off, and when the next is due.");

    app.MapPost("/access-reviews", async (ISender sender) =>
    {
      var result = await sender.Send(new StartAccessReviewCommand());
      return result.IsSuccess ? Results.Ok(new StartAccessReviewResponse(result.Value)) : Results.BadRequest(new { result.Message });
    })
      .RequirePermission(PermissionCatalog.Users.Assign)
      .WithName("StartAccessReview")
      .Produces<StartAccessReviewResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status400BadRequest);

    app.MapPut("/access-reviews/{id}/decisions/{userId}", async (Guid id, Guid userId, DecideAccessReviewRequest request, ISender sender) =>
    {
      var result = await sender.Send(new DecideAccessReviewCommand(id, userId, request.Outcome, request.Note));
      return result.IsSuccess ? Results.Ok(new AccessReviewDecisionResponse(result.Value!)) : Results.BadRequest(new { result.Message });
    })
      .RequirePermission(PermissionCatalog.Users.Assign)
      .WithName("DecideAccessReview")
      .Produces<AccessReviewDecisionResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithDescription("Kept, Changed (say what) or SwitchedOff (switches the account off now, with the usual guard rails). Not your own account.");

    app.MapPost("/access-reviews/{id}/close", async (Guid id, CloseAccessReviewRequest request, ISender sender) =>
    {
      var result = await sender.Send(new CloseAccessReviewCommand(id, request.Note));
      return result.IsSuccess ? Results.NoContent() : Results.BadRequest(new { result.Message });
    })
      .RequirePermission(PermissionCatalog.Users.Assign)
      .WithName("CloseAccessReview")
      .Produces(StatusCodes.Status204NoContent)
      .ProducesProblem(StatusCodes.Status400BadRequest);
  }
}

public sealed record OversightResponse(OversightDto Oversight);
public sealed record AccessReportResponse(IReadOnlyList<AccessReportRowDto> Accounts);
public sealed record ConflictsResponse(IReadOnlyList<PermissionConflictDto> Conflicts);
public sealed record OpenSessionsResponse(IReadOnlyList<OpenSessionDto> Sessions);
public sealed record OversightSettingsResponse(OversightSettingsDto Settings);
public sealed record AccessReviewsResponse(AccessReviewsDto Reviews);
public sealed record StartAccessReviewResponse(Guid Id);
public sealed record DecideAccessReviewRequest(ReviewOutcome Outcome, string? Note);
public sealed record AccessReviewDecisionResponse(AccessReviewDecisionDto Decision);
public sealed record CloseAccessReviewRequest(string? Note);
