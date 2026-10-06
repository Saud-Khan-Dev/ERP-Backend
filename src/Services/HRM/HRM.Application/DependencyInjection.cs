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

    services.Configure<DocumentOptions>(configuration.GetSection(DocumentOptions.SectionName));
    services.Configure<HrmOptions>(configuration.GetSection(HrmOptions.SectionName));
    services.Configure<PayrollOptions>(configuration.GetSection(PayrollOptions.SectionName));

    services.AddSingleton(TimeProvider.System);
    services.AddSingleton<IClock, PakistanClock>();

    services.AddScoped<CodeIssuer>();
    services.AddScoped<HrLookup>();
    services.AddScoped<ServiceRecordReader>();
    services.AddScoped<CurrentEmployee>();
    services.AddScoped<PayService>();
    services.AddScoped<FileUploads>();
    services.AddScoped<ServiceRecordWriter>();
    services.AddScoped<HrActionApplier>();
    services.AddScoped<WorkCalendar>();
    services.AddScoped<LeaveService>();
    services.AddScoped<PayrollInputBuilder>();
    services.AddScoped<PayrollEngine>();
    services.AddScoped<PayslipBuilder>();

    return services;
  }
}
