using Microsoft.EntityFrameworkCore;

public class GetUserHandler(IApplicationDbContext context, IUserPermissionService permissionService)
  : IQueryHandler<GetUserQuery, Result<GetUserQueryResult>>
{
  public async Task<Result<GetUserQueryResult>> Handle(GetUserQuery query, CancellationToken cancellationToken)
  {
    var now = DateTime.UtcNow;
    var id = UserId.Of(query.Id);

    var user = await context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, cancellationToken)
      ?? throw new UserNotFoundException($"User {query.Id} was not found.");

    var roles = await context.UserRoles.AsNoTracking()
        .Where(ur => ur.UserId == id)
        .Join(context.Roles.AsNoTracking(), ur => ur.RoleId, r => r.Id, (ur, r) => new { ur, r })
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
        x.ur.AssignedAt, x.ur.AssignedBy, x.ur.ExpiresAt, x.ur.IsLive(now))).ToList(),
      overrides.Select(x => new UserPermissionOverrideDto(
        x.p.Id.Value, x.p.Code.Value, x.o.Effect.ToString().ToUpperInvariant(),
        x.o.GrantedAt, x.o.GrantedBy, x.o.ExpiresAt, x.o.Reason, x.o.IsLive(now))).ToList(),
      resolved.PermissionCodes.OrderBy(p => p, StringComparer.Ordinal).ToList()));
  }
}
