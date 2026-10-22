/// Security audit.
public class SecurityEndpoints : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapGet("/security/login-attempts", async (
      ISender sender, int? pageIndex, int? pageSize, Guid? userId, string? username, bool? succeeded,
      DateTime? from, DateTime? to, string? reason) =>
    {
      var result = await sender.Send(new GetLoginAttemptsQuery(
        new PaginationRequest(pageIndex ?? 0, pageSize ?? 50), userId, username, succeeded, from, to,
        string.IsNullOrWhiteSpace(reason) ? null : reason.Trim()));

      return Results.Ok(new GetLoginAttemptsResponse(result.Value!.Attempts));
    })
      .RequirePermission(PermissionCatalog.Audit.View)
      .WithName("GetLoginAttempts")
      .Produces<GetLoginAttemptsResponse>(StatusCodes.Status200OK)
      .WithSummary("Get Login Attempts")
      .WithDescription("Every sign-in attempt, successful or not — including attempts against usernames that do not exist. Filter by user, outcome, reason (wrong-password, unknown-username, locked, deactivated, deleted) or date range to investigate a suspected attack.");
  }
}

public sealed record GetLoginAttemptsResponse(PaginatedResult<LoginAttemptDto> Attempts);
