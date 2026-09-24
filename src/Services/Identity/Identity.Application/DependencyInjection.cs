using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
  public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
  {
    services.AddMediatR(cfg =>
    {
      cfg.RegisterServicesFromAssemblies(Assembly.GetExecutingAssembly());
      cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
      cfg.AddOpenBehavior(typeof(LoggingBehaviors<,>));
    });

    services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

    services.Configure<SecurityOptions>(configuration.GetSection(SecurityOptions.SectionName));

    services.AddScoped<IUserPermissionService, UserPermissionService>();
    services.AddScoped<IdentityGuard>();

    return services;
  }
}
