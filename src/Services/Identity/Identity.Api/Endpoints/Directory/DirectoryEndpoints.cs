/// Reads every module needs about accounts: names behind stored user ids, and what waits in Administration for the
/// caller (the bell). Any signed-in person; names only, nothing about access.
public class DirectoryEndpoints : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapGet("/directory/users", async (ISender sender, string? ids, string? search, string? permission) =>
    {
      var parsed = new List<Guid>();
      foreach (var part in (ids ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
      {
        if (!Guid.TryParse(part, out var id))
          return Results.BadRequest(new { Message = "ids must be account ids, separated by commas." });
        parsed.Add(id);
      }
      var result = await sender.Send(new GetDirectoryUsersQuery(parsed, string.IsNullOrWhiteSpace(search) ? null : search.Trim(),
        string.IsNullOrWhiteSpace(permission) ? null : permission.Trim()));
      return Results.Ok(new DirectoryUsersResponse(result.Value!.Users));
    })
      .RequireSignedIn()
      .WithName("GetDirectoryUsers")
      .Produces<DirectoryUsersResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Get Account Names")
      .WithDescription("Display names for account ids (ids=a,b - at most 200; deleted accounts are named too, history must read), a name search (search=) over accounts in use, or the accounts in use that hold a permission now (permission=HR.APPROVE - who can act on something). Any signed-in person: names only.");

    app.MapGet("/attention", async (ISender sender) =>
    {
      var result = await sender.Send(new GetAttentionQuery());
      return Results.Ok(new AttentionResponse(result.Value!));
    })
      .RequireSignedIn()
      .WithName("GetAttention")
      .Produces<AttentionResponse>(StatusCodes.Status200OK)
      .WithSummary("What Waits In Administration")
      .WithDescription("Counts of what waits for the caller in Administration - only the queues the caller may act on (users.locked, users.temporary-password with IAM_USERS.EDIT; users.no-access, roles.ending with IAM_USERS.ASSIGN; signins.failed with IAM_SECURITY.VIEW). A zero count means checked, nothing waits.");
  }
}

public sealed record DirectoryUsersResponse(IReadOnlyList<DirectoryUserDto> Users);
