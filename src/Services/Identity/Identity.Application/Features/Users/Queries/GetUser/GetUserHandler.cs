using Microsoft.EntityFrameworkCore;

public class GetUserHandler(IApplicationDbContext context, IUserPermissionService permissionService)
  : IQueryHandler<GetUserQuery, Result<GetUserQueryResult>>
{
  public async Task<Result<GetUserQueryResult>> Handle(GetUserQuery query, CancellationToken cancellationToken)
  {
    var now = DateTime.UtcNow;
    var id = UserId.Of(query.Id);

    var users = query.IncludeDeleted ? context.Users.IgnoreQueryFilters() : context.Users;
    var user = await users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, cancellationToken)
      ?? throw new UserNotFoundException("This account was not found - it may have been deleted.");

    // grants in force and grants that ran out; a removed grant is history (Activity), not something the person holds
    var roles = await context.UserRoles.AsNoTracking()
        .Where(ur => ur.UserId == id && ur.RevokedAt == null)
        .Join(context.Roles.AsNoTracking(), ur => ur.RoleId, r => r.Id, (ur, r) => new { ur, r })
        .OrderBy(x => x.r.RoleName)
        .ToListAsync(cancellationToken);

    var overrides = await context.UserPermissionOverrides.AsNoTracking()
        .Where(o => o.UserId == id)
        .Join(context.Permissions.AsNoTracking(), o => o.PermissionId, p => p.Id, (o, p) => new { o, p })
        .ToListAsync(cancellationToken);

    var resolved = await permissionService.ResolveAsync(id, cancellationToken);

    return Result<GetUserQueryResult>.Success(new GetUserQueryResult(
      user.ToDto(now),
      roles.Select(x => new UserRoleDto(
        x.r.Id.Value, x.r.Code.Value, x.r.RoleName.Value,
        x.ur.AssignedAt, x.ur.AssignedBy, x.ur.ExpiresAt, x.ur.IsLive(now), x.r.IsActive)).ToList(),
      overrides.Select(x => new UserPermissionOverrideDto(
        x.p.Id.Value, x.p.Code.Value, x.o.Effect.ToString().ToUpperInvariant(),
        x.o.GrantedAt, x.o.GrantedBy, x.o.ExpiresAt, x.o.Reason, x.o.IsLive(now))).ToList(),
      resolved.PermissionCodes.OrderBy(p => p, StringComparer.Ordinal).ToList()));
  }
}
