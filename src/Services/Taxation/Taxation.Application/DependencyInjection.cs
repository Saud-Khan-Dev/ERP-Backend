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

    services.AddSingleton(TimeProvider.System);
    services.AddSingleton<IClock, PakistanClock>();

    services.AddScoped<TaxSetup>();
    services.AddScoped<Ledger>();
    services.AddScoped<Assessor>();
    services.AddScoped<CodeIssuer>();
    services.AddScoped<CalendarProvider>();
    services.AddScoped<Messenger>();
    services.AddScoped<NoticeComposer>();
    services.AddScoped<TaxFileImporter>();
    services.AddScoped<PenaltyService>();

    return services;
  }
}
