using System.Text.Json.Serialization;
using Carter;

public static class DependencyInjection
{
  public static IServiceCollection AddApiServices(this IServiceCollection services)
  {
    services.ConfigureHttpJsonOptions(options =>
    {
      options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

    services.AddCarter();
    services.AddOpenApi();
    services.AddExceptionHandler<AssetExceptionHandler>();
    services.AddProblemDetails();

    return services;
  }

  public static WebApplication UseApiService(this WebApplication app)
  {
    app.UseExceptionHandler(options => { });
    app.MapCarter();
    app.MapOpenApi();

    return app;
  }
}
