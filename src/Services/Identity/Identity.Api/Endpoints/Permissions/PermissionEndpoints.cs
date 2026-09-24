/// The permission catalogue an administrator picks from when building a role.
///
/// Read-only by design: permissions are seeded from the shared PermissionCatalog that the business
/// services reference, so the two can never drift apart. Adding one is a code change, not a data entry.
public class PermissionEndpoints : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapGet("/permission-modules", async (ISender sender) =>
    {
      var result = await sender.Send(new GetPermissionModulesQuery());
      return Results.Ok(new GetPermissionModulesResponse(result.Value!.Modules));
    })
      .RequirePermission(PermissionCatalog.Permissions.View)
      .WithName("GetPermissionModules")
      .Produces<GetPermissionModulesResponse>(StatusCodes.Status200OK)
      .WithSummary("Get Permission Modules")
      .WithDescription("The business areas permissions are grouped by: ASSETS, INVENTORY, PROPERTY, IAM_USERS ...");

    app.MapGet("/permissions", async (ISender sender, string? module, bool? includeInactive) =>
    {
      var result = await sender.Send(new GetPermissionsQuery(module, includeInactive ?? false));
      return Results.Ok(new GetPermissionsResponse(result.Value!.Permissions));
    })
      .RequirePermission(PermissionCatalog.Permissions.View)
      .WithName("GetPermissions")
      .Produces<GetPermissionsResponse>(StatusCodes.Status200OK)
      .WithSummary("Get Permissions")
      .WithDescription("Every MODULE.ACTION permission, optionally filtered to one module.");
  }
}

public sealed record GetPermissionModulesResponse(IReadOnlyList<PermissionModuleDto> Modules);
public sealed record GetPermissionsResponse(IReadOnlyList<PermissionDto> Permissions);
