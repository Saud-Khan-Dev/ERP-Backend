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
      // Shared ERP database: this service owns the "auth" schema and its own migrations history.
      opt.UseNpgsql(connectionString, npgsql =>
        npgsql.MigrationsHistoryTable("__EFMigrationsHistory", ApplicationDbContext.Schema));
      opt.UseSnakeCaseNamingConvention();
    });

    services.AddScoped<IApplicationDbContext, ApplicationDbContext>();

    // ---- security ----
    services.AddDataProtection();
    services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
    services.AddSingleton<IPasswordGenerator, RandomPasswordGenerator>();
    services.AddSingleton<ISecretProtector, DataProtectionSecretProtector>();
    services.AddScoped<ITokenService, JwtTokenService>();

    // ---- bootstrap ----
    services.Configure<SeedOptions>(configuration.GetSection(SeedOptions.SectionName));
    services.AddScoped<IdentitySeeder>();
    services.AddScoped<DatabaseInitializer>();

    return services;
  }
}
