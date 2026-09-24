using System.Text.Json.Serialization;
using Carter;

public static class DependencyInjection
{
  public static IServiceCollection AddApiServices(this IServiceCollection services, IConfiguration configuration)
  {
    services.ConfigureHttpJsonOptions(options =>
    {
      options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

    // JWT validation + permission policies + ICurrentUser, from the shared building block
    services.AddErpAuthentication(configuration);

    services.AddCarter();
    services.AddOpenApi();
    services.AddExceptionHandler<IdentityExceptionHandler>();
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
    app.MapOpenApi();

    return app;
  }
}

public static class HttpContextExtensions
{
  /// Caller IP for the audit trail. Read from the connection, never from a client-supplied header.
  public static string? CallerIp(this HttpContext context) =>
      context.Connection.RemoteIpAddress?.ToString();

  public static string? CallerUserAgent(this HttpContext context) =>
      context.Request.Headers.UserAgent.ToString() is { Length: > 0 } agent ? agent : null;
}
