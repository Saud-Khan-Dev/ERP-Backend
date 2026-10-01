using System.Text.Json.Serialization;
using Carter;
using Microsoft.AspNetCore.Http.Features;

public static class DependencyInjection
{
  public static IServiceCollection AddApiServices(this IServiceCollection services)
  {
    services.ConfigureHttpJsonOptions(options =>
    {
      options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

    // uploads up to the attachment limit (25 MB) plus form overhead
    services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = 30 * 1024 * 1024);

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
