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
    services.AddScoped<ISaveChangesInterceptor, AuditLogInterceptor>();

    services.AddDbContext<ApplicationDbContext>((sp, opt) =>
    {
      opt.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>()!);
      // Shared ERP database: this service owns the "taxation" schema and its own migrations history.
      opt.UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", ApplicationDbContext.Schema));
      // schema naming: tax_file_basis, effective_from ...
      opt.UseSnakeCaseNamingConvention();
    });

    services.AddScoped<IApplicationDbContext, ApplicationDbContext>();
    services.AddScoped<INumberSource, PostgresNumberSource>();

    // ---- files ----
    services.Configure<FileStorageOptions>(configuration.GetSection(FileStorageOptions.SectionName));
    services.AddSingleton<IFileStorage, LocalFileStorage>();

    // ---- the other services, called with the signed-in user's token ----
    var endpoints = configuration.GetSection(ServiceEndpointsOptions.SectionName).Get<ServiceEndpointsOptions>() ?? new ServiceEndpointsOptions();
    services.AddHttpContextAccessor();
    services.AddTransient<ForwardAuthorizationHandler>();
    services.AddHttpClient<IPropertyDirectory, PropertyServiceClient>(http => Configure(http, endpoints.PropertyBaseUrl, endpoints.TimeoutSeconds))
      .AddHttpMessageHandler<ForwardAuthorizationHandler>();
    services.AddHttpClient<IHrmDirectory, HrmServiceClient>(http => Configure(http, endpoints.HrmBaseUrl, endpoints.TimeoutSeconds))
      .AddHttpMessageHandler<ForwardAuthorizationHandler>();

    // ---- bootstrap ----
    services.AddScoped<TaxationSeeder>();
    services.AddScoped<DatabaseInitializer>();

    return services;
  }

  /// A base address ending in "/" so relative paths ("properties/{id}") resolve under it. No address: the client is not
  /// configured (HRM is optional; Property is required to open or sync a file).
  private static void Configure(HttpClient http, string? baseUrl, int timeoutSeconds)
  {
    if (!string.IsNullOrWhiteSpace(baseUrl))
      http.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
    http.Timeout = TimeSpan.FromSeconds(Math.Clamp(timeoutSeconds, 2, 120));
  }
}
