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

    services.AddDbContext<ApplicationDbContext>((sp, opt) =>
    {
      opt.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>()!);
      // Shared ERP database: this service owns the "hrms" schema and its own migrations history.
      opt.UseNpgsql(connectionString, npgsql =>
      {
        npgsql.MigrationsHistoryTable("__EFMigrationsHistory", ApplicationDbContext.Schema);
        HrmsEnumMapping.Apply(npgsql);
      });
      // schema naming: organization_unit_version, effective_from ...
      opt.UseSnakeCaseNamingConvention();
    });

    services.AddScoped<IApplicationDbContext, ApplicationDbContext>();

    // ---- files ----
    services.Configure<FileStorageOptions>(configuration.GetSection(FileStorageOptions.SectionName));
    services.AddSingleton<IFileStorage, LocalFileStorage>();

    // ---- bootstrap ----
    services.AddScoped<HrmSeeder>();
    services.AddScoped<DatabaseInitializer>();

    return services;
  }
}
