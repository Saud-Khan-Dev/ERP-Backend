/// User account administration — the Super Admin provisioning workflow.
///
/// Every route carries an explicit permission. There is no anonymous registration endpoint here,
/// and no route accepts a role name as a string: roles are referenced by id and authorized server-side.
public class UserEndpoints : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapPost("/users", async (CreateUserRequest request, ISender sender) =>
    {
      var result = await sender.Send(new CreateUserCommand(request.User));

      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });

      var response = result.Value.Adapt<CreateUserResponse>();
      return Results.Created($"/users/{response!.Id}", response);
    })
      .RequirePermission(PermissionCatalog.Users.Create)
      .WithName("CreateUser")
      .Produces<CreateUserResponse>(StatusCodes.Status201Created)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Register An Employee Account")
      .WithDescription("Creates a login account, links it to an employee and assigns roles in one call. Omit temporaryPassword to have one generated — it is returned once and never retrievable again.");

    app.MapGet("/users", async (
      ISender sender, int? pageIndex, int? pageSize, string? search, Guid? roleId, bool? isActive, bool? includeDeleted) =>
    {
      var result = await sender.Send(new GetUsersQuery(
        new PaginationRequest(pageIndex ?? 0, pageSize ?? 20), search, roleId, isActive, includeDeleted ?? false));

      return Results.Ok(new GetUsersResponse(result.Value!.Users));
    })
      .RequirePermission(PermissionCatalog.Users.View)
      .WithName("GetUsers")
      .Produces<GetUsersResponse>(StatusCodes.Status200OK)
      .WithSummary("Get Users")
      .WithDescription("Paginated account list with search and role/status filters.");

    app.MapGet("/users/{id}", async (Guid id, ISender sender) =>
    {
      var result = await sender.Send(new GetUserQuery(id));

      return Results.Ok(new GetUserResponse(
        result.Value!.User, result.Value.Roles, result.Value.Overrides, result.Value.EffectivePermissions));
    })
      .RequirePermission(PermissionCatalog.Users.View)
      .WithName("GetUser")
      .Produces<GetUserResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Get User")
      .WithDescription("Profile, role grants, permission overrides, and the permission set they add up to.");

    app.MapPut("/users/{id}", async (Guid id, UpdateUserRequest request, ISender sender) =>
    {
      var result = await sender.Send(new UpdateUserCommand(id, request.User));

      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });

      return Results.Ok(result.Value.Adapt<UpdateUserResponse>());
    })
      .RequirePermission(PermissionCatalog.Users.Edit)
      .WithName("UpdateUser")
      .Produces<UpdateUserResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Update User")
      .WithDescription("Profile fields only. Roles, activation and passwords each have their own endpoint so every privilege change is explicit.");

    app.MapPost("/users/{id}/activate", async (Guid id, ISender sender) =>
    {
      var result = await sender.Send(new SetUserActivationCommand(id, true));
      return Results.Ok(result.Value.Adapt<SetUserActivationResponse>());
    })
      .RequirePermission(PermissionCatalog.Users.Edit)
      .WithName("ActivateUser")
      .Produces<SetUserActivationResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Activate User")
      .WithDescription("Re-enables sign-in for an account.");

    app.MapPost("/users/{id}/deactivate", async (Guid id, ISender sender) =>
    {
      var result = await sender.Send(new SetUserActivationCommand(id, false));
      return Results.Ok(result.Value.Adapt<SetUserActivationResponse>());
    })
      .RequirePermission(PermissionCatalog.Users.Edit)
      .WithName("DeactivateUser")
      .Produces<SetUserActivationResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Deactivate User")
      .WithDescription("Blocks sign-in and revokes every live session. Refuses on your own account, or on the last active Super Admin.");

    app.MapPost("/users/{id}/unlock", async (Guid id, ISender sender) =>
    {
      var result = await sender.Send(new UnlockUserCommand(id));
      return Results.Ok(result.Value.Adapt<UnlockUserResponse>());
    })
      .RequirePermission(PermissionCatalog.Users.Edit)
      .WithName("UnlockUser")
      .Produces<UnlockUserResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Unlock User")
      .WithDescription("Clears a brute-force lockout without changing the password.");

    app.MapPost("/users/{id}/reset-password", async (Guid id, ResetPasswordRequest? request, ISender sender) =>
    {
      var result = await sender.Send(new ResetUserPasswordCommand(id, request?.NewPassword));
      return Results.Ok(new ResetPasswordResponse(result.Value!.GeneratedPassword));
    })
      .RequirePermission(PermissionCatalog.Users.Edit)
      .WithName("ResetUserPassword")
      .Produces<ResetPasswordResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Reset User Password")
      .WithDescription("Sets a new password, forces a change at next sign-in and revokes every session. Omit newPassword to have one generated and returned once.");

    app.MapPost("/users/{id}/revoke-sessions", async (Guid id, ISender sender) =>
    {
      var result = await sender.Send(new RevokeUserSessionsCommand(id));
      return Results.Ok(result.Value.Adapt<RevokeUserSessionsResponse>());
    })
      .RequirePermission(PermissionCatalog.Security.Revoke)
      .WithName("RevokeUserSessions")
      .Produces<RevokeUserSessionsResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Force Logout")
      .WithDescription("Signs a user out of every device.");

    app.MapGet("/users/{id}/sessions", async (Guid id, ISender sender, bool? includeRevoked) =>
    {
      var result = await sender.Send(new GetUserSessionsQuery(id, includeRevoked ?? false));
      return Results.Ok(new UserSessionsResponse(result.Value!.Sessions));
    })
      .RequirePermission(PermissionCatalog.Security.View)
      .WithName("GetUserSessions")
      .Produces<UserSessionsResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Get User Sessions")
      .WithDescription("Which devices a user is signed in on.");

    app.MapDelete("/users/{id}", async (Guid id, ISender sender) =>
    {
      var result = await sender.Send(new DeleteUserCommand(id));
      return Results.Ok(result.Value.Adapt<DeleteUserResponse>());
    })
      .RequirePermission(PermissionCatalog.Users.Delete)
      .WithName("DeleteUser")
      .Produces<DeleteUserResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Delete User")
      .WithDescription("Soft delete: login stops immediately, but the account row survives so login history and audit references stay resolvable.");

    // =====================================================
    // ROLES AND OVERRIDES ON A USER
    // =====================================================

    app.MapPost("/users/{id}/roles", async (Guid id, AssignRoleRequest request, ISender sender) =>
    {
      var result = await sender.Send(new AssignRoleToUserCommand(id, request.RoleId, request.ExpiresAt));

      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });

      return Results.Ok(result.Value.Adapt<AssignRoleResponse>());
    })
      .RequirePermission(PermissionCatalog.Users.Assign)
      .WithName("AssignRoleToUser")
      .Produces<AssignRoleResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Assign Role")
      .WithDescription("Grants a role, optionally until a date. Refuses on your own account, and only an existing Super Admin may grant SUPER_ADMIN.");

    app.MapDelete("/users/{id}/roles/{roleId}", async (Guid id, Guid roleId, ISender sender) =>
    {
      var result = await sender.Send(new RemoveRoleFromUserCommand(id, roleId));
      return Results.Ok(result.Value.Adapt<RemoveRoleResponse>());
    })
      .RequirePermission(PermissionCatalog.Users.Assign)
      .WithName("RemoveRoleFromUser")
      .Produces<RemoveRoleResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Remove Role")
      .WithDescription("Revokes a role grant, keeping both the grant and its removal in the audit trail.");

    app.MapPost("/users/{id}/permission-overrides", async (Guid id, SetOverrideRequest request, ISender sender) =>
    {
      var result = await sender.Send(new SetPermissionOverrideCommand(
        id, request.PermissionId, request.Effect, request.ExpiresAt, request.Reason));

      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });

      return Results.Ok(result.Value.Adapt<SetOverrideResponse>());
    })
      .RequirePermission(PermissionCatalog.Permissions.Assign)
      .WithName("SetUserPermissionOverride")
      .Produces<SetOverrideResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Set Permission Override")
      .WithDescription("Grants one extra permission to a single user (Allow), or withholds one their role gives (Deny). Deny always beats a role grant.");

    app.MapDelete("/users/{id}/permission-overrides/{permissionId}", async (Guid id, Guid permissionId, ISender sender) =>
    {
      var result = await sender.Send(new RemovePermissionOverrideCommand(id, permissionId));
      return Results.Ok(result.Value.Adapt<RemoveOverrideResponse>());
    })
      .RequirePermission(PermissionCatalog.Permissions.Assign)
      .WithName("RemoveUserPermissionOverride")
      .Produces<RemoveOverrideResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Remove Permission Override")
      .WithDescription("Drops the override so the user falls back to what their roles grant.");
  }
}

public sealed record CreateUserRequest(CreateUserInput User);
public sealed record CreateUserResponse(Guid Id, string? GeneratedPassword);

public sealed record GetUsersResponse(PaginatedResult<UserListItemDto> Users);

public sealed record GetUserResponse(
  UserDto User,
  IReadOnlyList<UserRoleDto> Roles,
  IReadOnlyList<UserPermissionOverrideDto> Overrides,
  IReadOnlyList<string> EffectivePermissions);

public sealed record UpdateUserRequest(UpdateUserInput User);
public sealed record UpdateUserResponse(bool IsSuccess);

public sealed record SetUserActivationResponse(bool IsActive);
public sealed record UnlockUserResponse(bool IsSuccess);

public sealed record ResetPasswordRequest(string? NewPassword);
public sealed record ResetPasswordResponse(string? GeneratedPassword);

public sealed record RevokeUserSessionsResponse(int RevokedCount);
public sealed record UserSessionsResponse(IReadOnlyList<SessionDto> Sessions);
public sealed record DeleteUserResponse(bool IsSuccess);

public sealed record AssignRoleRequest(Guid RoleId, DateTime? ExpiresAt = null);
public sealed record AssignRoleResponse(Guid UserRoleId);
public sealed record RemoveRoleResponse(bool IsSuccess);

public sealed record SetOverrideRequest(Guid PermissionId, OverrideEffect Effect, DateTime? ExpiresAt = null, string? Reason = null);
public sealed record SetOverrideResponse(bool IsSuccess);
public sealed record RemoveOverrideResponse(bool IsSuccess);
