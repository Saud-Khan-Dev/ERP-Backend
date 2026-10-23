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

    // the shared audit trail (hrms.audit_entry): every record added, changed or removed, who and when
    services.AddAuditTrail<ApplicationDbContext>(HrmAuditProfile.Module, HrmAuditProfile.Configure);

    services.AddDbContext<ApplicationDbContext>((sp, opt) =>
    {
      opt.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>()!);
      opt.AddInterceptors(sp.GetRequiredService<AuditTrailInterceptor>());
      // Shared ERP database: this service owns the "hrms" schema and its own migrations history.
      opt.UseNpgsql(connectionString, npgsql =>
      {
        npgsql.MigrationsHistoryTable("__EFMigrationsHistory", ApplicationDbContext.Schema);
        HrmsEnumMapping.Apply(npgsql);
      });
      // schema naming: organization_unit_version, effective_from ...
      opt.UseSnakeCaseNamingConvention();
    });

    services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

    // the bulk and scheduled work HRM runs, for Administration › System
    services.AddScoped<ISystemJobSource, HrmJobSource>();

    // ---- files ----
    services.Configure<FileStorageOptions>(configuration.GetSection(FileStorageOptions.SectionName));
    services.AddSingleton<IFileStorage, LocalFileStorage>();

    // ---- bootstrap ----
    services.AddScoped<HrmSeeder>();
    services.AddScoped<DatabaseInitializer>();

    return services;
  }
}
