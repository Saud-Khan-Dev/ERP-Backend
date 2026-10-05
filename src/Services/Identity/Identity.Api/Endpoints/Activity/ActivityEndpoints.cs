/// The administration activity trail - who changed what in Administration, for review.
public class ActivityEndpoints : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapGet("/admin-activity", async (
        ISender sender, int? pageIndex, int? pageSize,
        Guid? actorUserId, Guid? targetId, string? action, DateTime? from, DateTime? to) =>
    {
      var result = await sender.Send(new GetAdminActivityQuery(
        new PaginationRequest(pageIndex ?? 0, pageSize ?? 20),
        actorUserId, targetId, action, from, to));

      return Results.Ok(new GetAdminActivityResponse(result.Value!.Activities));
    })
      .RequirePermission(PermissionCatalog.Security.View)
      .WithName("GetAdminActivity")
      .Produces<GetAdminActivityResponse>(StatusCodes.Status200OK)
      .WithSummary("Get Administration Activity")
      .WithDescription("Who changed what in Administration - role grants, account changes, permission changes, configuration edits - newest first, filterable by actor, target, kind and date.");
  }
}

public sealed record GetAdminActivityResponse(PaginatedResult<AdminActivityDto> Activities);
