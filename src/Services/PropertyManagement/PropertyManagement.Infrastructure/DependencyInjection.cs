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
      // Shared ERP database: this service owns the "property" schema and its own migrations history.
      opt.UseNpgsql(connectionString, npgsql =>
        npgsql.MigrationsHistoryTable("__EFMigrationsHistory", ApplicationDbContext.Schema));
      // schema naming: property_status_history, ownership_share_pct ...
      opt.UseSnakeCaseNamingConvention();
    });

    services.AddScoped<IApplicationDbContext, ApplicationDbContext>();

    // ---- files ----
    services.Configure<FileStorageOptions>(configuration.GetSection(FileStorageOptions.SectionName));
    services.AddSingleton<IFileStorage, LocalFileStorage>();

    // ---- bootstrap ----
    services.AddScoped<PropertySeeder>();
    services.AddScoped<DatabaseInitializer>();

    return services;
  }
}
