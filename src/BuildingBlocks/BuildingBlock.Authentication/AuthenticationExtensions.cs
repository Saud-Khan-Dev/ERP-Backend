using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

/// Wires JWT validation and permission-based authorization into any service.
///
/// Every service calls this — including the ones behind the gateway, because a service must stay
/// safe when it is reached directly on its own port.
public static class AuthenticationExtensions
{
  public static IServiceCollection AddErpAuthentication(this IServiceCollection services, IConfiguration configuration)
  {
    var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

    if (string.IsNullOrWhiteSpace(jwt.SigningKey))
      throw new InvalidOperationException(
        $"{JwtOptions.SectionName}:SigningKey is not configured. Set it through configuration or the " +
        $"{JwtOptions.SectionName}__SigningKey environment variable.");

    services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
    services.Configure<ErpServiceUrls>(configuration.GetSection(ErpServiceUrls.SectionName));

    services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
      .AddJwtBearer(options =>
      {
        // Keep the short claim names the issuer writes ("role", "sub", "ao"...). With the default mapping
        // "role" arrives as the long ClaimTypes.Role URI and ICurrentUser.Roles comes back empty.
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
          ValidateIssuer = true,
          ValidIssuer = jwt.Issuer,
          ValidateAudience = true,
          ValidAudience = jwt.Audience,
          ValidateIssuerSigningKey = true,
          IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
          ValidateLifetime = true,
          // tokens are short-lived; don't hand out an extra five minutes of validity
          ClockSkew = TimeSpan.FromSeconds(30),
          NameClaimType = ErpClaimTypes.Username,
          RoleClaimType = ErpClaimTypes.Role
        };
        options.Events = new JwtBearerEvents
        {
          // a valid signature is not enough: the session must still be open, the account on, its access unchanged
          OnTokenValidated = async context =>
          {
            var raw = context.SecurityToken switch
            {
              JsonWebToken json => json.EncodedToken,
              _ => context.Request.Headers.Authorization.ToString().Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase)
            };
            var checker = context.HttpContext.RequestServices.GetRequiredService<ITokenStateChecker>();
            var verdict = await checker.CheckAsync(context.Principal!, raw, context.HttpContext.RequestAborted);
            if (!verdict.Current)
            {
              context.HttpContext.Items[StaleReasonKey] = verdict.Reason;
              context.Fail(verdict.Reason ?? "The sign-in is no longer current.");
            }
          },
          // the screen refreshes its token on a 401; the reason says why the old one was refused
          OnChallenge = context =>
          {
            if (context.HttpContext.Items[StaleReasonKey] is string reason)
              context.Response.Headers["X-Token-Stale"] = Uri.EscapeDataString(reason);
            return Task.CompletedTask;
          }
        };
      });

    services.AddAuthorization(options =>
    {
      // `.RequireAuthorization()` means a signed-in person whose temporary password has been changed; a token still
      // waiting for that change opens only the routes that say so (Identity's own /auth routes)
      options.DefaultPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .RequireAssertion(context => !context.User.HasClaim(c => c.Type == ErpClaimTypes.PasswordChange))
        .Build();
      options.AddPolicy(TemporaryPasswordAllowedPolicy, policy => policy.RequireAuthenticatedUser());
    });
    services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
    services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();

    services.AddHttpContextAccessor();
    services.AddScoped<ICurrentUser, CurrentUser>();
    services.AddScoped<IRequestOrigin, RequestOrigin>();

    // Identity registers its own (database) checker and duty rules before this call; the other services ask Identity
    services.AddMemoryCache();
    services.AddHttpClient(HttpTokenStateChecker.ClientName, (provider, client) =>
    {
      var urls = provider.GetRequiredService<IOptions<ErpServiceUrls>>().Value;
      client.BaseAddress = new Uri(urls.IdentityBaseUrl.TrimEnd('/') + "/");
      client.Timeout = TimeSpan.FromSeconds(3);
    });
    services.TryAddSingleton<ITokenStateChecker, HttpTokenStateChecker>();
    services.TryAddSingleton<IDutyRules, HttpDutyRules>();

    // the person's own address, not the gateway's: honour X-Forwarded-For from the private network the gateway runs on
    services.Configure<ForwardedHeadersOptions>(options =>
    {
      options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
      options.ForwardLimit = 2;
      options.KnownIPNetworks.Clear();
      options.KnownProxies.Clear();
      foreach (var network in new[] { "10.0.0.0/8", "172.16.0.0/12", "192.168.0.0/16", "127.0.0.0/8", "::1/128", "fc00::/7" })
        options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
    });
    services.AddTransient<IStartupFilter, ForwardedHeadersStartupFilter>();

    return services;
  }

  /// For Identity's own routes a temporary-password token must reach (change password, who am I, sign out).
  public const string TemporaryPasswordAllowedPolicy = "erp:temporary-password-allowed";

  private const string StaleReasonKey = "erp:token-stale-reason";

  /// Puts UseForwardedHeaders first in every service's pipeline, so every later step - the audit trail, the sign-in
  /// log - reads the caller's own address.
  private sealed class ForwardedHeadersStartupFilter : IStartupFilter
  {
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
      app.UseForwardedHeaders();
      next(app);
    };
  }

  /// Requires the caller to hold `permissionCode`.
  /// Unauthenticated callers get 401; authenticated callers without the permission get 403.
  public static RouteHandlerBuilder RequirePermission(this RouteHandlerBuilder builder, string permissionCode) =>
      builder
        .RequireAuthorization($"{PermissionPolicyProvider.PolicyPrefix}{permissionCode}")
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

  /// Same, for a whole route group.
  public static RouteGroupBuilder RequirePermission(this RouteGroupBuilder builder, string permissionCode) =>
      builder.RequireAuthorization($"{PermissionPolicyProvider.PolicyPrefix}{permissionCode}");

  /// The caller needs at least one of the permissions (401 without a token, 403 without any of them) - for a screen
  /// several jobs open, e.g. the lists a module's configuration keeps, read by its clerks and by its administrators.
  public static RouteHandlerBuilder RequireAnyPermission(this RouteHandlerBuilder builder, params string[] permissionCodes) =>
      builder
        .RequireAuthorization(policy => policy
          .RequireAuthenticatedUser()
          .RequireAssertion(context => permissionCodes.Any(code => context.User.HasClaim(ErpClaimTypes.Permission, code))))
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

  /// Same, for a whole route group.
  public static RouteGroupBuilder RequireAnyPermission(this RouteGroupBuilder builder, params string[] permissionCodes) =>
      builder.RequireAuthorization(policy => policy
        .RequireAuthenticatedUser()
        .RequireAssertion(context => permissionCodes.Any(code => context.User.HasClaim(ErpClaimTypes.Permission, code))));

  /// Any signed-in person - for directory reads every module needs (who holds a post, an employee's name), never for
  /// anything that changes data. A token still waiting for its temporary password to be changed is refused (403).
  public static RouteHandlerBuilder RequireSignedIn(this RouteHandlerBuilder builder) =>
      builder
        .RequireAuthorization(policy => policy
          .RequireAuthenticatedUser()
          .RequireAssertion(context => !context.User.HasClaim(c => c.Type == ErpClaimTypes.PasswordChange)))
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

  /// Same, for a whole route group (the employee portal's own records).
  public static RouteGroupBuilder RequireSignedIn(this RouteGroupBuilder builder) =>
      builder.RequireAuthorization(policy => policy
        .RequireAuthenticatedUser()
        .RequireAssertion(context => !context.User.HasClaim(c => c.Type == ErpClaimTypes.PasswordChange)));
}
