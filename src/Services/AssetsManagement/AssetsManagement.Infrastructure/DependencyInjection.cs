using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
  public static IServiceCollection AddInfrastructureService(this IServiceCollection services, IConfiguration configuration)
  {
    var connectionString = configuration.GetConnectionString("Database");
    services.AddScoped<ISaveChangesInterceptor, AuditableEntityInterceptors>();
    services.AddScoped<ISaveChangesInterceptor, DispatchDomainEventInterceptor>();

    // the shared audit trail (assets.audit_entry, append-only): every save writes who changed what, in words
    services.AddAuditTrail<ApplicationDbContext>(AssetAuditKinds.Module, AssetsAuditProfile.Configure);
    // Administration › System: the month-end depreciation run
    services.AddScoped<ISystemJobSource, DepreciationRunJobSource>();

    services.AddDbContext<ApplicationDbContext>((sp, opt) =>
    {
      // the trail goes last, so it sees the rows as the other interceptors left them
      opt.AddInterceptors([.. sp.GetServices<ISaveChangesInterceptor>(), sp.GetRequiredService<AuditTrailInterceptor>()]);
      // Shared ERP database: this service owns the "assets" schema and its own migrations history,
      // so it never collides with PropertyManagement's tables in "public".
      opt.UseNpgsql(connectionString, npgsql =>
        npgsql.MigrationsHistoryTable("__EFMigrationsHistory", ApplicationDbContext.Schema));
      // ERD naming: asset_class, attribute_definition, extra_attributes ...
      opt.UseSnakeCaseNamingConvention();
    });

    // one context per request: the handlers' IApplicationDbContext IS the ApplicationDbContext the audit trail writes to,
    // so the lines an act records are saved in the same transaction as the act
    services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

    // documents and photos of assets: the file server as a directory (a mounted volume in Docker)
    services.Configure<FileStorageOptions>(configuration.GetSection(FileStorageOptions.SectionName));
    services.Configure<AttachmentOptions>(configuration.GetSection(AttachmentOptions.SectionName));
    services.AddSingleton<IFileStorage, LocalFileStorage>();

    services.AddSingleton<IClock, OrganisationClock>();
    services.AddScoped<IAssetDataQueries, AssetDataQueries>();

    // ---- HRM's staff directory, called with the signed-in user's token ----
    var endpoints = configuration.GetSection(ServiceEndpointsOptions.SectionName).Get<ServiceEndpointsOptions>() ?? new ServiceEndpointsOptions();
    services.AddHttpContextAccessor();
    services.AddTransient<ForwardAuthorizationHandler>();
    services.AddHttpClient<IHrmDirectory, HrmDirectoryClient>(http =>
    {
      if (!string.IsNullOrWhiteSpace(endpoints.HrmBaseUrl))
        http.BaseAddress = new Uri(endpoints.HrmBaseUrl.TrimEnd('/') + "/");
      http.Timeout = TimeSpan.FromSeconds(Math.Clamp(endpoints.TimeoutSeconds, 2, 120));
    }).AddHttpMessageHandler<ForwardAuthorizationHandler>();

    return services;
  }
}
