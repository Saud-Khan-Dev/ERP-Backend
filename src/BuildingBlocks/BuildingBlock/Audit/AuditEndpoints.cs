using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

/// What a trail listing is filtered by (see AuditPageDto for the meaning of each).
public sealed record AuditFilter(
  DateTimeOffset? Before = null,
  DateOnly? From = null,
  DateOnly? To = null,
  Guid? ActorId = null,
  string? Action = null,
  string? Kind = null,
  Guid? RecordId = null,
  Guid? ParentId = null,
  string? Search = null,
  int? PageSize = null);

public static class AuditQueries
{
  /// Pakistan Standard Time (UTC+5, no daylight saving): a day in the filters is a GDA working day.
  private static readonly TimeSpan Pkt = TimeSpan.FromHours(5);

  /// The trail, newest first, filtered - for `GET /audit` and for a module's own record-history endpoints.
  public static async Task<AuditPageDto> ListAsync(IQueryable<AuditRecord> trail, AuditFilter filter, CancellationToken cancellationToken)
  {
    var query = trail.AsNoTracking();
    if (filter.From is { } from)
      query = query.Where(x => x.At >= new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), Pkt));
    if (filter.To is { } to)
      query = query.Where(x => x.At < new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue), Pkt));
    if (filter.ActorId is { } actor)
      query = query.Where(x => x.ActorId == actor);
    if (!string.IsNullOrWhiteSpace(filter.Action))
      query = query.Where(x => x.Action == filter.Action);
    if (!string.IsNullOrWhiteSpace(filter.Kind))
      query = query.Where(x => x.RecordKind == filter.Kind);
    if (filter.RecordId is { } record)
      query = query.Where(x => x.RecordId == record);
    if (filter.ParentId is { } parent)
      query = query.Where(x => x.ParentId == parent || x.RecordId == parent);
    if (!string.IsNullOrWhiteSpace(filter.Search))
    {
      var text = filter.Search.Trim().ToLower();
      query = query.Where(x => x.RecordKind.ToLower().Contains(text)
        || (x.RecordCode != null && x.RecordCode.ToLower().Contains(text))
        || (x.RecordLabel != null && x.RecordLabel.ToLower().Contains(text))
        || (x.ParentCode != null && x.ParentCode.ToLower().Contains(text))
        || (x.Field != null && x.Field.ToLower().Contains(text))
        || (x.OldValue != null && x.OldValue.ToLower().Contains(text))
        || (x.NewValue != null && x.NewValue.ToLower().Contains(text))
        || (x.Detail != null && x.Detail.ToLower().Contains(text))
        || x.ActorName.ToLower().Contains(text));
    }

    var count = await query.LongCountAsync(cancellationToken);
    if (filter.Before is { } before)
      query = query.Where(x => x.At < before);
    var size = Math.Clamp(filter.PageSize ?? 50, 1, 200);
    var rows = await query.OrderByDescending(x => x.At).ThenByDescending(x => x.Id).Take(size).ToListAsync(cancellationToken);
    return new AuditPageDto(rows.Select(r => r.ToDto()).ToList(), count);
  }

  public static AuditFilter FilterFrom(HttpRequest request)
  {
    string? Text(string name) => request.Query.TryGetValue(name, out var v) && !string.IsNullOrWhiteSpace(v) ? v.ToString() : null;
    Guid? Id(string name) => Guid.TryParse(Text(name), out var g) ? g : null;
    DateOnly? Day(string name) => DateOnly.TryParse(Text(name), out var d) ? d : null;
    return new AuditFilter(
      DateTimeOffset.TryParse(Text("before"), out var before) ? before : null,
      Day("from"), Day("to"), Id("actorId"), Text("action"), Text("kind"), Id("recordId"), Id("parentId"), Text("search"),
      int.TryParse(Text("pageSize"), out var size) ? size : null);
  }
}

public static class AuditEndpoints
{
  /// The module's trail for Administration (`GET /audit`, `GET /audit/kinds`, IAM_AUDIT.VIEW) and the line a screen
  /// writes when it exports or prints (`POST /audit/exports`, any of `exportPermissions`).
  public static IEndpointRouteBuilder MapAuditTrail<TContext>(this IEndpointRouteBuilder app, params string[] exportPermissions) where TContext : DbContext
  {
    app.MapGet("/audit", async (HttpRequest request, TContext context, CancellationToken cancellationToken) =>
        Results.Ok(new AuditResponse(await AuditQueries.ListAsync(context.Set<AuditRecord>(), AuditQueries.FilterFrom(request), cancellationToken))))
      .RequirePermission(PermissionCatalog.Audit.View)
      .WithTags("Audit trail")
      .WithName("GetAuditTrail")
      .WithSummary("The module's audit trail, newest first")
      .Produces<AuditResponse>();

    app.MapGet("/audit/kinds", async (TContext context, CancellationToken cancellationToken) =>
      {
        var trail = context.Set<AuditRecord>().AsNoTracking();
        var kinds = await trail.Select(x => x.RecordKind).Distinct().OrderBy(x => x).ToListAsync(cancellationToken);
        var actions = await trail.Select(x => x.Action).Distinct().OrderBy(x => x).ToListAsync(cancellationToken);
        return Results.Ok(new AuditKindsResponse(kinds, actions));
      })
      .RequirePermission(PermissionCatalog.Audit.View)
      .WithTags("Audit trail")
      .WithName("GetAuditKinds")
      .Produces<AuditKindsResponse>();

    var export = app.MapPost("/audit/exports", async (RecordExportRequest body, IAuditTrail audit, CancellationToken cancellationToken) =>
      {
        if (string.IsNullOrWhiteSpace(body.What))
          return Results.BadRequest(new { message = "Say what was exported." });
        var action = body.Format?.ToLowerInvariant() switch
        {
          "print" => AuditActions.Printed,
          "download" => AuditActions.Downloaded,
          _ => AuditActions.Exported
        };
        var detail = string.Join(" · ", new[] { $"{body.Rows} row{(body.Rows == 1 ? "" : "s")}", body.Filters }.Where(x => !string.IsNullOrWhiteSpace(x)));
        audit.Record(action, body.What.Trim(), body.RecordId, body.RecordCode, null, body.RecordId, null, detail);
        await audit.SaveAsync(cancellationToken);
        return Results.NoContent();
      })
      .WithTags("Audit trail")
      .WithName("RecordExport")
      .WithSummary("Note in the trail that a file was exported or a page printed")
      .Produces(StatusCodes.Status204NoContent);
    if (exportPermissions.Length > 0)
      export.RequireAnyPermission(exportPermissions);
    else
      export.RequireAuthorization();

    return app;
  }
}
