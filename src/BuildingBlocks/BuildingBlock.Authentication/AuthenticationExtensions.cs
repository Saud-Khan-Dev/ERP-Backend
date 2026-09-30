using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
      });

    services.AddAuthorization();
    services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
    services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();

    services.AddHttpContextAccessor();
    services.AddScoped<ICurrentUser, CurrentUser>();

    return services;
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
}
