using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
  public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
  {
    var connectionString = configuration.GetConnectionString("Database");

    services.AddScoped<ISaveChangesInterceptor, AuditableEntityInterceptors>();
    services.AddScoped<ISaveChangesInterceptor, DispatchDomainEventInterceptor>();

    // the shared audit trail (audit_entry): every record added, changed or removed, in the same save; the old
    // property_change log is kept read-only and no longer written (PropertyChangeLogInterceptor is gone)
    services.AddAuditTrail<ApplicationDbContext>(PropertyAudit.Module, PropertyAudit.Configure);

    services.AddDbContext<ApplicationDbContext>((sp, opt) =>
    {
      // the trail last, so it sees what the other interceptors changed
      opt.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>().Append(sp.GetRequiredService<AuditTrailInterceptor>()));
      // Shared ERP database: this service owns the "property" schema and its own migrations history.
      opt.UseNpgsql(connectionString, npgsql =>
        npgsql.MigrationsHistoryTable("__EFMigrationsHistory", ApplicationDbContext.Schema));
      // schema naming: property_status_history, ownership_share_pct ...
      opt.UseSnakeCaseNamingConvention();
    });

    // the same instance the audit trail writes to, so lines recorded with IAuditTrail.Record are saved with the handler's SaveChanges
    services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

    // ---- files ----
    services.Configure<FileStorageOptions>(configuration.GetSection(FileStorageOptions.SectionName));
    services.AddSingleton<IFileStorage, LocalFileStorage>();

    // ---- bootstrap ----
    services.AddScoped<PropertySeeder>();
    services.AddScoped<DatabaseInitializer>();

    return services;
  }
}
