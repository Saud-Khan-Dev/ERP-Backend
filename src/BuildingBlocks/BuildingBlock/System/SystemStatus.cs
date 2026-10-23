using System.Diagnostics;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

/// A scheduled or bulk job a service runs, as the system page shows it.
/// LastResult: "Succeeded", "Failed" or "Running"; Detail: what the last run did or why it failed, in words.
public sealed record SystemJobDto(string Name, DateTimeOffset? LastRunAt, string? LastResult, string? Detail, DateTimeOffset? NextRunAt);

/// A service's jobs (register one per service that runs any).
public interface ISystemJobSource
{
  Task<IReadOnlyList<SystemJobDto>> JobsAsync(CancellationToken cancellationToken);
}

/// `GET /system/status` (IAM_SYSTEM.VIEW): one service's health for Administration › System.
/// Database: whether the service reaches its schema, how long a query took, migrations not applied yet.
public sealed record SystemStatusDto(
  string Service,
  string Version,
  DateTimeOffset StartedAt,
  string Environment,
  bool DatabaseOk,
  long? DatabaseMilliseconds,
  int PendingMigrations,
  long AuditLines,
  DateTimeOffset? LastAuditAt,
  IReadOnlyList<SystemJobDto> Jobs);

public sealed record SystemStatusResponse(SystemStatusDto Status);

public static class SystemStatusEndpoints
{
  private static readonly DateTimeOffset Started = DateTimeOffset.UtcNow;

  public static IEndpointRouteBuilder MapSystemStatus<TContext>(this IEndpointRouteBuilder app, string service) where TContext : DbContext
  {
    app.MapGet("/system/status", async (TContext context, IServiceProvider services, IWebHostEnvironment environment, CancellationToken cancellationToken) =>
      {
        var version = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
          ?? Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown";
        bool ok;
        long? elapsed = null;
        var pending = 0;
        long lines = 0;
        DateTimeOffset? lastAudit = null;
        try
        {
          var watch = Stopwatch.StartNew();
          ok = await context.Database.CanConnectAsync(cancellationToken);
          elapsed = watch.ElapsedMilliseconds;
          if (ok)
          {
            pending = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).Count();
            // a service that keeps its trail elsewhere (Identity's activity) has no audit_entry
            if (context.Model.FindEntityType(typeof(AuditRecord)) is not null)
            {
              var trail = context.Set<AuditRecord>().AsNoTracking();
              lines = await trail.LongCountAsync(cancellationToken);
              lastAudit = await trail.OrderByDescending(x => x.At).Select(x => (DateTimeOffset?)x.At).FirstOrDefaultAsync(cancellationToken);
            }
          }
        }
        catch (Exception)
        {
          ok = false;
        }
        var jobs = new List<SystemJobDto>();
        foreach (var source in services.GetServices<ISystemJobSource>())
          jobs.AddRange(await source.JobsAsync(cancellationToken));
        return Results.Ok(new SystemStatusResponse(new SystemStatusDto(service, version, Started, environment.EnvironmentName, ok, elapsed, pending, lines,
          lastAudit, jobs)));
      })
      .RequirePermission(PermissionCatalog.SystemAdmin.View)
      .WithTags("System")
      .WithName("GetSystemStatus")
      .Produces<SystemStatusResponse>();
    return app;
  }
}
