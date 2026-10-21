using System.Text.Json.Serialization;
using Carter;
using Microsoft.AspNetCore.Http.Features;

public static class DependencyInjection
{
  public static IServiceCollection AddApiServices(this IServiceCollection services, IConfiguration configuration)
  {
    services.ConfigureHttpJsonOptions(options =>
    {
      options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

    // JWT validation + permission policies + ICurrentUser, from the shared building block. Every endpoint declares
    // the permission it needs (AssetsAuthorization lists the reader set the structure and the lists share).
    services.AddErpAuthentication(configuration);

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

    // order matters: authenticate, then authorize, then route to the endpoint
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapCarter();

    // the API description is for developers (npm run sync:types) - not served outside Development
    if (app.Environment.IsDevelopment())
      app.MapOpenApi();

    return app;
  }
}

/// Result<T> → HTTP: success is 200 with the value, a business-rule failure is 400 { message }.
public static class ResultHttpExtensions
{
  public static IResult ToOk<T>(this Result<T> result) =>
      result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(new { result.Message });
}
