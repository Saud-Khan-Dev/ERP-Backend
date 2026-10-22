/// Authentication and self-service.
///
/// The endpoints up to and including /auth/refresh are anonymous by necessity — they are how a
/// caller obtains an identity in the first place. Everything below them requires a valid token but
/// no particular permission: they only ever act on the caller's own account.
public class AuthEndpoints : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    // =====================================================
    // ANONYMOUS
    // =====================================================

    app.MapPost("/auth/login", async (LoginRequest request, HttpContext http, ISender sender) =>
    {
      // IP and user agent come from the connection, never from the request body
      var result = await sender.Send(new LoginCommand(
        request.Username, request.Password, http.CallerIp(), http.CallerUserAgent()));

      return Results.Ok(new LoginResponse(result.Value!.Authentication));
    })
      .AllowAnonymous()
      .RequireRateLimiting(DependencyInjection.SignInRateLimit)
      .WithName("Login")
      .Produces<LoginResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status401Unauthorized)
      .ProducesProblem(StatusCodes.Status403Forbidden)
      .WithSummary("Login")
      .WithDescription("Exchanges a username (or email) and password for an access token and a refresh token. Returns 401 for bad credentials and 403 when the account is inactive or locked.");

    app.MapPost("/auth/refresh", async (RefreshRequest request, HttpContext http, ISender sender) =>
    {
      var result = await sender.Send(new RefreshTokenCommand(
        request.RefreshToken, http.CallerIp(), http.CallerUserAgent()));

      return Results.Ok(new RefreshResponse(result.Value!.Authentication));
    })
      .AllowAnonymous()
      .WithName("RefreshToken")
      .Produces<RefreshResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status401Unauthorized)
      .WithSummary("Refresh Token")
      .WithDescription("Issues a new access token and rotates the refresh token. Presenting an already-rotated token revokes that sign-in's chain (a second use within 30 seconds is refused without revoking anything).");

    app.MapGet("/auth/password-policy", async (ISender sender) =>
    {
      var result = await sender.Send(new GetPasswordPolicyQuery());
      return Results.Ok(new PasswordPolicyResponse(result.Value!));
    })
      .AllowAnonymous()
      .WithName("GetPasswordPolicy")
      .Produces<PasswordPolicyResponse>(StatusCodes.Status200OK)
      .WithSummary("Get The Password Rules")
      .WithDescription("The password rules in force, for every password form's checklist (anonymous: the change-password and first sign-in screens need them).");

    app.MapPost("/auth/logout", async (LogoutRequest request, ISender sender) =>
    {
      var result = await sender.Send(new LogoutCommand(request.RefreshToken, request.AllSessions));
      return Results.Ok(result.Value.Adapt<LogoutResponse>());
    })
      .AllowAnonymous()
      .WithName("Logout")
      .Produces<LogoutResponse>(StatusCodes.Status200OK)
      .WithSummary("Logout")
      .WithDescription("Revokes the session behind the refresh token. Idempotent, and never reveals whether the token existed.");

    // =====================================================
    // AUTHENTICATED — the caller's own account only
    // =====================================================

    // every service asks this (with the caller's token) whether a token is still current; the token check itself
    // runs before the endpoint (DatabaseTokenStateChecker), so reaching it means yes
    app.MapGet("/auth/token-state", () => Results.Ok(new TokenStateResponse(true, null)))
      .RequireAuthorization(AuthenticationExtensions.TemporaryPasswordAllowedPolicy)
      .WithName("GetTokenState")
      .Produces<TokenStateResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status401Unauthorized)
      .WithSummary("Is This Token Current")
      .WithDescription("200 when the sign-in is open, the account on and the token's access stamp current; 401 (with X-Token-Stale saying why) otherwise. Called by every service, cached 30 seconds.");

    app.MapGet("/auth/me", async (ISender sender) =>
    {
      var result = await sender.Send(new GetMeQuery());
      return Results.Ok(new MeResponse(result.Value!.User));
    })
      .RequireAuthorization(AuthenticationExtensions.TemporaryPasswordAllowedPolicy)
      .WithName("GetMe")
      .Produces<MeResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status401Unauthorized)
      .WithSummary("Get My Identity")
      .WithDescription("The caller's identity, roles and effective permissions — resolved live, so a role change is visible immediately. A UI uses this to hide actions; the backend still enforces them.");

    app.MapPost("/auth/change-password", async (ChangePasswordRequest request, ISender sender) =>
    {
      var result = await sender.Send(new ChangePasswordCommand(request.CurrentPassword, request.NewPassword));

      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });

      return Results.Ok(result.Value.Adapt<ChangePasswordResponse>());
    })
      .RequireAuthorization(AuthenticationExtensions.TemporaryPasswordAllowedPolicy)
      .WithName("ChangePassword")
      .Produces<ChangePasswordResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status401Unauthorized)
      .WithSummary("Change My Password")
      .WithDescription("Changes the caller's own password and signs every session out, including this one.");

    app.MapGet("/auth/sessions", async (ISender sender, bool? includeRevoked) =>
    {
      var result = await sender.Send(new GetMySessionsQuery(includeRevoked ?? false));
      return Results.Ok(new MySessionsResponse(result.Value!.Sessions));
    })
      .RequireAuthorization()
      .WithName("GetMySessions")
      .Produces<MySessionsResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status401Unauthorized)
      .WithSummary("Get My Sessions")
      .WithDescription("The devices the caller is currently signed in on.");

    app.MapPost("/auth/sessions/revoke-others", async (ISender sender) =>
    {
      var result = await sender.Send(new RevokeOtherSessionsCommand());
      return Results.Ok(new RevokeOtherSessionsResponse(result.Value!.RevokedCount));
    })
      .RequireAuthorization()
      .WithName("RevokeMyOtherSessions")
      .Produces<RevokeOtherSessionsResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status401Unauthorized)
      .WithSummary("Sign Out My Other Devices")
      .WithDescription("Signs out every device of the caller except the one making this request.");

    app.MapPost("/auth/sessions/{id}/revoke", async (Guid id, ISender sender) =>
    {
      var result = await sender.Send(new RevokeMySessionCommand(id));
      return Results.Ok(result.Value.Adapt<RevokeMySessionResponse>());
    })
      .RequireAuthorization()
      .WithName("RevokeMySession")
      .Produces<RevokeMySessionResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status401Unauthorized)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Revoke One Of My Sessions")
      .WithDescription("Signs one of the caller's own devices out. Scoped to the caller, so another user's session cannot be revoked by guessing an id.");
  }
}

public sealed record LoginRequest(string Username, string Password);
public sealed record LoginResponse(AuthenticationResultDto Authentication);

public sealed record RefreshRequest(string RefreshToken);
public sealed record RefreshResponse(AuthenticationResultDto Authentication);

public sealed record LogoutRequest(string RefreshToken, bool AllSessions = false);
public sealed record LogoutResponse(bool IsSuccess);

public sealed record MeResponse(CurrentUserDto User);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public sealed record ChangePasswordResponse(bool IsSuccess);

public sealed record MySessionsResponse(IReadOnlyList<SessionDto> Sessions);

public sealed record RevokeMySessionResponse(bool IsSuccess);

public sealed record PasswordPolicyResponse(PasswordPolicyDto Policy);
public sealed record RevokeOtherSessionsResponse(int RevokedCount);
