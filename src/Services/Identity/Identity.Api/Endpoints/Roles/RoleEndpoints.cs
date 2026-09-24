/// Role administration. A role is a bundle of permissions; users are given roles, never permissions
/// directly (except through the deliberate override mechanism).
public class RoleEndpoints : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapPost("/roles", async (CreateRoleRequest request, ISender sender) =>
    {
      var result = await sender.Send(new CreateRoleCommand(request.Role));

      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });

      var response = result.Value.Adapt<CreateRoleResponse>();
      return Results.Created($"/roles/{response!.Id}", response);
    })
      .RequirePermission(PermissionCatalog.Roles.Create)
      .WithName("CreateRole")
      .Produces<CreateRoleResponse>(StatusCodes.Status201Created)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Create Role")
      .WithDescription("Creates a role, optionally with its initial permissions. Roles created here are never system roles.");

    app.MapGet("/roles", async (ISender sender, bool? includeInactive) =>
    {
      var result = await sender.Send(new GetRolesQuery(includeInactive ?? false));
      return Results.Ok(new GetRolesResponse(result.Value!.Roles));
    })
      .RequirePermission(PermissionCatalog.Roles.View)
      .WithName("GetRoles")
      .Produces<GetRolesResponse>(StatusCodes.Status200OK)
      .WithSummary("Get Roles")
      .WithDescription("All roles with how many permissions each carries.");

    app.MapGet("/roles/{id}", async (Guid id, ISender sender) =>
    {
      var result = await sender.Send(new GetRoleQuery(id));
      return Results.Ok(new GetRoleResponse(result.Value!.Role, result.Value.Permissions));
    })
      .RequirePermission(PermissionCatalog.Roles.View)
      .WithName("GetRole")
      .Produces<GetRoleResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Get Role")
      .WithDescription("The role together with every permission attached to it.");

    app.MapPut("/roles/{id}", async (Guid id, UpdateRoleRequest request, ISender sender) =>
    {
      var result = await sender.Send(new UpdateRoleCommand(
        id, request.Code, request.Name, request.Description, request.IsActive));

      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });

      return Results.Ok(result.Value.Adapt<UpdateRoleResponse>());
    })
      .RequirePermission(PermissionCatalog.Roles.Edit)
      .WithName("UpdateRole")
      .Produces<UpdateRoleResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Update Role")
      .WithDescription("System roles cannot be renamed or deactivated.");

    app.MapDelete("/roles/{id}", async (Guid id, ISender sender) =>
    {
      var result = await sender.Send(new DeleteRoleCommand(id));

      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });

      return Results.Ok(result.Value.Adapt<DeleteRoleResponse>());
    })
      .RequirePermission(PermissionCatalog.Roles.Delete)
      .WithName("DeleteRole")
      .Produces<DeleteRoleResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Delete Role")
      .WithDescription("Soft-deletes a non-system role that nobody currently holds.");

    app.MapPost("/roles/{id}/permissions", async (Guid id, AddRolePermissionsRequest request, ISender sender) =>
    {
      var result = await sender.Send(new AddRolePermissionsCommand(id, request.PermissionIds));
      return Results.Ok(result.Value.Adapt<AddRolePermissionsResponse>());
    })
      .RequirePermission(PermissionCatalog.Permissions.Assign)
      .WithName("AddRolePermissions")
      .Produces<AddRolePermissionsResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Add Permissions To Role")
      .WithDescription("Attaches permissions to a role; already-attached ones are ignored, so the call is safe to repeat.");

    app.MapDelete("/roles/{id}/permissions/{permissionId}", async (Guid id, Guid permissionId, ISender sender) =>
    {
      var result = await sender.Send(new RemoveRolePermissionCommand(id, permissionId));

      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });

      return Results.Ok(result.Value.Adapt<RemoveRolePermissionResponse>());
    })
      .RequirePermission(PermissionCatalog.Permissions.Assign)
      .WithName("RemoveRolePermission")
      .Produces<RemoveRolePermissionResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Remove Permission From Role")
      .WithDescription("Detaches a permission. The built-in SUPER_ADMIN role cannot be stripped, or nobody could administer the system again.");
  }
}

public sealed record CreateRoleRequest(RoleInput Role);
public sealed record CreateRoleResponse(Guid Id);

public sealed record GetRolesResponse(IReadOnlyList<RoleDto> Roles);
public sealed record GetRoleResponse(RoleDto Role, IReadOnlyList<PermissionDto> Permissions);

public sealed record UpdateRoleRequest(string Code, string Name, string? Description, bool IsActive = true);
public sealed record UpdateRoleResponse(bool IsSuccess);

public sealed record DeleteRoleResponse(bool IsSuccess);

public sealed record AddRolePermissionsRequest(IReadOnlyList<Guid> PermissionIds);
public sealed record AddRolePermissionsResponse(int AddedCount);
public sealed record RemoveRolePermissionResponse(bool IsSuccess);
