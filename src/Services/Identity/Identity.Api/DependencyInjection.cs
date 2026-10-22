using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Carter;

public static class DependencyInjection
{
  /// At most 60 sign-in attempts a minute from one address.
  public const string SignInRateLimit = "sign-in";

  public static IServiceCollection AddApiServices(this IServiceCollection services, IConfiguration configuration)
  {
    services.ConfigureHttpJsonOptions(options =>
    {
      options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

    // JWT validation + permission policies + ICurrentUser, from the shared building block
    services.AddErpAuthentication(configuration);

    // password spraying across many accounts from one address: the per-account lockout cannot see it, this can
    services.AddRateLimiter(options =>
    {
      options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
      options.AddPolicy(SignInRateLimit, http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
      options.OnRejected = async (context, token) =>
      {
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsync("{\"message\":\"Too many sign-in attempts from this address. Wait a minute and try again.\"}", token);
      };
    });

    services.AddHostedService<DormantAccountsService>();

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
    app.UseRateLimiter();

    app.MapCarter();
    app.MapSystemStatus<ApplicationDbContext>("Administration");
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
