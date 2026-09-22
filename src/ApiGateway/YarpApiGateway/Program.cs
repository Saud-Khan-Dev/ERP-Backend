using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.AddRateLimiter(options =>
{
  // 429 is the standard "slow down" answer; the default 503 looks like an outage to a UI
  options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

  // per client IP so one heavy user cannot starve everyone else
  options.AddPolicy("fixed", context =>
    RateLimitPartition.GetFixedWindowLimiter(
      context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
      _ => new FixedWindowRateLimiterOptions
      {
        Window = TimeSpan.FromSeconds(10),
        PermitLimit = builder.Configuration.GetValue<int?>("RateLimiting:PermitLimitPer10Seconds") ?? 100
      }));
});

var app = builder.Build();

app.UseRateLimiter();
app.MapReverseProxy();

app.Run();
