using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
  public static IServiceCollection AddApplicationservices(this IServiceCollection services)
  {
    services.AddMediatR(cfg =>
    {
      cfg.RegisterServicesFromAssemblies(Assembly.GetExecutingAssembly());
      cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
      cfg.AddOpenBehavior(typeof(LoggingBehaviors<,>));
    });

    services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

    services.AddScoped<IAttributeSchemaService, AttributeSchemaService>();

    return services;
  }
}
