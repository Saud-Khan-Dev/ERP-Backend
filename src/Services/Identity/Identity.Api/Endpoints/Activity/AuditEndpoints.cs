/// Administration's part of the all-modules audit trail (the same `/audit` shape every service answers), and the line an
/// Administration screen writes when it exports a register.
public class AuditEndpoints : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapGet("/audit", async (HttpRequest request, ISender sender) =>
    {
      var result = await sender.Send(new GetAdministrationAuditQuery(AuditQueries.FilterFrom(request)));
      return Results.Ok(new AuditResponse(result.Value!));
    })
      .RequirePermission(PermissionCatalog.Audit.View)
      .WithTags("Audit trail")
      .WithName("GetAuditTrail")
      .Produces<AuditResponse>(StatusCodes.Status200OK)
      .WithSummary("Administration's Audit Trail")
      .WithDescription("Every administrative act as an audit line (module Administration), newest first; the filters every service's /audit takes.");

    app.MapGet("/audit/kinds", () => Results.Ok(new AuditKindsResponse(
        Enum.GetValues<ActivityTargetType>().Select(GetAdministrationAuditHandler.KindWords).ToList(),
        Enum.GetValues<ActivityAction>().Select(GetAdministrationAuditHandler.ActionWords).OrderBy(x => x).ToList())))
      .RequirePermission(PermissionCatalog.Audit.View)
      .WithTags("Audit trail")
      .WithName("GetAuditKinds")
      .Produces<AuditKindsResponse>(StatusCodes.Status200OK);

    app.MapPost("/audit/exports", async (RecordExportRequest body, ISender sender) =>
    {
      var result = await sender.Send(new RecordAdministrationExportCommand(body));
      return result.IsSuccess ? Results.NoContent() : Results.BadRequest(new { result.Message });
    })
      .RequireAnyPermission(PermissionCatalog.Audit.Export, PermissionCatalog.Users.Export)
      .WithTags("Audit trail")
      .WithName("RecordExport")
      .Produces(StatusCodes.Status204NoContent);
  }
}
