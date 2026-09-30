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

    // JWT validation + permission policies + ICurrentUser, from the shared building block
    services.AddErpAuthentication(configuration);

    // document uploads: allow the configured file size plus room for the form fields
    var maxFileSize = configuration.GetValue<long?>($"{DocumentOptions.SectionName}:MaxFileSizeBytes") ?? new DocumentOptions().MaxFileSizeBytes;
    services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = maxFileSize + 1024 * 1024);

    services.AddCarter();
    services.AddOpenApi();
    services.AddExceptionHandler<PropertyExceptionHandler>();
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

/// Result<T> → HTTP: success is 200/201 with the value, a business-rule failure is 400 { message }.
public static class ResultHttpExtensions
{
  public static IResult ToOk<T>(this Result<T> result) =>
      result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(new { result.Message });

  public static IResult ToCreated<T>(this Result<T> result, Func<T, string> location) =>
      result.IsSuccess ? Results.Created(location(result.Value!), result.Value) : Results.BadRequest(new { result.Message });
}
