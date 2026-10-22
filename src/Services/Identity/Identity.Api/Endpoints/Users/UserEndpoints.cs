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
      .WithDescription("Creates a login account, optionally linked to the person's HR record (employeeId + HR's employeeCode from HRM's staff directory), and assigns roles in one call (giving roles needs IAM_USERS.ASSIGN too). Omit temporaryPassword to have one generated — it is returned once and never retrievable again. The person always changes it at first sign-in.");

    app.MapGet("/users", async (
      ISender sender, int? pageIndex, int? pageSize, string? search, Guid? roleId, bool? isActive, bool? includeDeleted,
      string? status, string? sort, string? direction) =>
    {
      var result = await sender.Send(new GetUsersQuery(
        new PaginationRequest(pageIndex ?? 0, pageSize ?? 20), search, roleId, isActive, includeDeleted ?? false,
        string.IsNullOrWhiteSpace(status) ? null : status.Trim(),
        string.IsNullOrWhiteSpace(sort) ? null : sort.Trim(),
        string.IsNullOrWhiteSpace(direction) ? null : direction.Trim()));

      return Results.Ok(new GetUsersResponse(result.Value!.Users));
    })
      .RequirePermission(PermissionCatalog.Users.View)
      .WithName("GetUsers")
      .Produces<GetUsersResponse>(StatusCodes.Status200OK)
      .WithSummary("Get Users")
      .WithDescription("The Users register: search (part of the name, username, email or employee code), status (active, locked, temporary-password, never-signed-in, no-access, deactivated, deleted), roleId (people who hold the role now), sort (name, username, lastSignIn, registered) and direction (asc, desc). Pages of at most 200.");

    app.MapGet("/users/summary", async (ISender sender) =>
    {
      var result = await sender.Send(new GetUsersSummaryQuery());
      return Results.Ok(new GetUsersSummaryResponse(result.Value!));
    })
      .RequirePermission(PermissionCatalog.Users.View)
      .WithName("GetUsersSummary")
      .Produces<GetUsersSummaryResponse>(StatusCodes.Status200OK)
      .WithSummary("Get Users Summary")
      .WithDescription("How many accounts are in each state the Users register filters by - every figure is exactly what the same status filter lists.");

    app.MapGet("/users/{id}", async (Guid id, ISender sender, bool? includeDeleted) =>
    {
      var result = await sender.Send(new GetUserQuery(id, includeDeleted ?? false));

      return Results.Ok(new GetUserResponse(
        result.Value!.User, result.Value.Roles, result.Value.Overrides, result.Value.EffectivePermissions, result.Value.Scope));
    })
      .RequirePermission(PermissionCatalog.Users.View)
      .WithName("GetUser")
      .Produces<GetUserResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Get User")
      .WithDescription("Profile, role grants (in force or run out; removed ones are in the activity trail), permission overrides, and the permission set they add up to. includeDeleted=true opens a deleted account read-only.");

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
      .WithDescription("Name and email. Roles, activation, passwords and the HR link (which brings the employee code) each have their own endpoint so every change of access or identity is explicit.");

    app.MapPost("/users/{id}/activate", async (Guid id, ISender sender) =>
    {
      var result = await sender.Send(new SetUserActivationCommand(id, true));
      return Results.Ok(result.Value.Adapt<SetUserActivationResponse>());
    })
      .RequirePermission(PermissionCatalog.Users.Disable)
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
      .RequirePermission(PermissionCatalog.Users.Disable)
      .WithName("DeactivateUser")
      .Produces<SetUserActivationResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Deactivate User")
      .WithDescription("Blocks sign-in and revokes every live session. Refuses on your own account, or on the last active Super Admin.");

    app.MapPost("/users/{id}/authorized-officer", async (Guid id, SetAuthorizedOfficerRequest request, ISender sender) =>
    {
      var result = await sender.Send(new SetAuthorizedOfficerCommand(id, request.IsAuthorizedOfficer));
      return Results.Ok(result.Value.Adapt<SetAuthorizedOfficerResponse>());
    })
      .RequirePermission(PermissionCatalog.Users.Assign)
      .WithName("SetAuthorizedOfficer")
      .Produces<SetAuthorizedOfficerResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Designate Authorized Officer")
      .WithDescription("Marks an account as an officer authorized by the DG (GDA Act s.2(a-i)): only such officers may impose fines (s.28) or file complaints in court (s.30). Refused on your own account; takes effect with the user's next access token.");

    app.MapPost("/users/{id}/unlock", async (Guid id, ISender sender) =>
    {
      var result = await sender.Send(new UnlockUserCommand(id));
      return Results.Ok(result.Value.Adapt<UnlockUserResponse>());
    })
      .RequirePermission(PermissionCatalog.Users.Disable)
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
      .RequirePermission(PermissionCatalog.Users.Reset)
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

    app.MapPut("/users/{id}/roles", async (Guid id, SetUserRolesRequest request, ISender sender) =>
    {
      var result = await sender.Send(new SetUserRolesCommand(id, request.Roles ?? [], request.ConflictReason));

      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });

      return Results.Ok(new SetUserRolesResponse(result.Value!.Added, result.Value.Removed));
    })
      .RequirePermission(PermissionCatalog.Users.Assign)
      .WithName("SetUserRoles")
      .Produces<SetUserRolesResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Change Roles")
      .WithDescription("Sets the exact roles a person holds (a role newly given may end on a date): kept roles are untouched, new ones given, missing ones taken away - one act, one activity line. The same guard rails as giving one role: not on your own account, only a Super Admin acts on a Super Admin or gives that role, nobody gives access they do not hold, and the last Super Admin keeps the role.");

    app.MapPut("/users/{id}/scope", async (Guid id, SetUserScopeRequest request, ISender sender) =>
    {
      var result = await sender.Send(new SetUserScopeCommand(id, request.Towns ?? [], request.Offices ?? []));
      return result.IsSuccess ? Results.Ok(new UserScopeResponse(result.Value!)) : Results.BadRequest(new { Message = result.Message });
    })
      .RequirePermission(PermissionCatalog.Users.Assign)
      .WithName("SetUserScope")
      .Produces<UserScopeResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Set Where A Person Works")
      .WithDescription("The towns (Property and Taxation records) and HRM offices (with their sub-units) the person may see and act on; an empty list means all. Not your own account, not an account that can do more than you. Takes effect at once (the person's tokens are re-issued).");

    app.MapPut("/users/{id}/employee-link", async (Guid id, EmployeeLinkRequest request, ISender sender) =>
    {
      var result = await sender.Send(new LinkEmployeeCommand(id, request.EmployeeId, request.EmployeeCode));

      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });

      return Results.Ok(new EmployeeLinkResponse(result.Value!.EmployeeId, result.Value.EmployeeCode));
    })
      .RequirePermission(PermissionCatalog.Users.Edit)
      .WithName("LinkUserToEmployee")
      .Produces<EmployeeLinkResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Link Account To HR Record")
      .WithDescription("Links the account to the person's HR record (employeeId from HRM's staff directory) and shows HR's employee number (employeeCode) as the account's code; both null unlink it. One HR record has one account. HRM keeps its own side (POST /employees/{id}/user); the token's emp claim follows on the person's next sign-in or refresh.");

    app.MapPost("/users/{id}/restore", async (Guid id, ISender sender) =>
    {
      var result = await sender.Send(new RestoreUserCommand(id));
      return Results.Ok(new RestoreUserResponse(result.Value!.IsSuccess));
    })
      .RequirePermission(PermissionCatalog.Users.Delete)
      .WithName("RestoreUser")
      .Produces<RestoreUserResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Restore Deleted User")
      .WithDescription("Brings a deleted account back, switched off - review its roles, then activate it.");

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
      .RequirePermission(PermissionCatalog.Permissions.Override)
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
      .RequirePermission(PermissionCatalog.Permissions.Override)
      .WithName("RemoveUserPermissionOverride")
      .Produces<RemoveOverrideResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Remove Permission Override")
      .WithDescription("Drops the override so the user falls back to what their roles grant.");
  }
}

public sealed record CreateUserRequest(CreateUserInput User);
public sealed record CreateUserResponse(Guid Id, string? GeneratedPassword, string? EmployeeCode);

public sealed record GetUsersResponse(PaginatedResult<UserListItemDto> Users);
public sealed record GetUsersSummaryResponse(UsersSummaryDto Summary);

public sealed record GetUserResponse(
  UserDto User,
  IReadOnlyList<UserRoleDto> Roles,
  IReadOnlyList<UserPermissionOverrideDto> Overrides,
  IReadOnlyList<string> EffectivePermissions,
  UserScopeDto Scope);

public sealed record UpdateUserRequest(UpdateUserInput User);
public sealed record UpdateUserResponse(bool IsSuccess);

public sealed record SetUserActivationResponse(bool IsActive);
public sealed record UnlockUserResponse(bool IsSuccess);
public sealed record SetAuthorizedOfficerRequest(bool IsAuthorizedOfficer);
public sealed record SetAuthorizedOfficerResponse(bool IsAuthorizedOfficer);

public sealed record ResetPasswordRequest(string? NewPassword);
public sealed record ResetPasswordResponse(string? GeneratedPassword);

public sealed record RevokeUserSessionsResponse(int RevokedCount);
public sealed record UserSessionsResponse(IReadOnlyList<SessionDto> Sessions);
public sealed record DeleteUserResponse(bool IsSuccess);

public sealed record AssignRoleRequest(Guid RoleId, DateTime? ExpiresAt = null);
public sealed record SetUserRolesRequest(IReadOnlyList<RoleGrantInput>? Roles, string? ConflictReason = null);
public sealed record SetUserRolesResponse(int Added, int Removed);
public sealed record EmployeeLinkRequest(Guid? EmployeeId, string? EmployeeCode);
public sealed record EmployeeLinkResponse(Guid? EmployeeId, string? EmployeeCode);
public sealed record RestoreUserResponse(bool IsSuccess);
public sealed record AssignRoleResponse(Guid UserRoleId);
public sealed record RemoveRoleResponse(bool IsSuccess);

public sealed record SetOverrideRequest(Guid PermissionId, OverrideEffect Effect, DateTime? ExpiresAt = null, string? Reason = null);
public sealed record SetOverrideResponse(bool IsSuccess);
public sealed record RemoveOverrideResponse(bool IsSuccess);

public sealed record SetUserScopeRequest(IReadOnlyList<ScopePlaceDto>? Towns, IReadOnlyList<ScopePlaceDto>? Offices);
public sealed record UserScopeResponse(UserScopeDto Scope);
