using Microsoft.AspNetCore.Http.Features;

public static class DependencyInjection
{
  public static IServiceCollection AddApiServices(this IServiceCollection services, IConfiguration configuration)
  {
    services.ConfigureHttpJsonOptions(options =>
    {
      // names out (as in the other services); names or the schema's snake_case labels in
      options.SerializerOptions.Converters.Add(new LenientEnumConverterFactory());
    });

    // JWT validation + permission policies + ICurrentUser, from the shared building block
    services.AddErpAuthentication(configuration);

    // logo uploads are small (1 MB) plus the form fields
    services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = 2 * 1024 * 1024);

    services.AddCarter();
    services.AddOpenApi();
    services.AddExceptionHandler<TaxationExceptionHandler>();
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
