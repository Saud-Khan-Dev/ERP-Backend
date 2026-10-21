using Microsoft.EntityFrameworkCore;

/// What waits in Administration for the caller - the bell and the Administration overview. Only the queues the caller
/// may act on are listed (a zero count means "checked, nothing waits"):
///
/// - `users.locked`              locked after wrong passwords, now (IAM_USERS.EDIT unlocks)
/// - `users.no-access`           active accounts that can open nothing (IAM_USERS.ASSIGN gives roles)
/// - `users.temporary-password`  active accounts still holding the password an administrator gave (IAM_USERS.EDIT)
/// - `signins.failed`            failed sign-ins in the last 24 hours (IAM_SECURITY.VIEW)
/// - `roles.ending`              people whose temporary role ends within 7 days (IAM_USERS.ASSIGN), `nextDue` the first
public sealed record GetAttentionQuery : IQuery<Result<AttentionDto>>;

public class GetAttentionHandler(IApplicationDbContext context, ICurrentUser currentUser) : IQueryHandler<GetAttentionQuery, Result<AttentionDto>>
{
  public async Task<Result<AttentionDto>> Handle(GetAttentionQuery query, CancellationToken cancellationToken)
  {
    var now = DateTime.UtcNow;
    var items = new List<AttentionItemDto>();
    var users = context.Users.AsNoTracking();

    if (currentUser.HasPermission(PermissionCatalog.Users.Edit))
    {
      var locked = users.Where(u => u.LockedUntil != null && u.LockedUntil > now);
      items.Add(new AttentionItemDto("users.locked", await locked.CountAsync(cancellationToken)));

      var temporary = users.Where(u => u.IsActive && u.MustChangePassword);
      var oldest = await temporary.MinAsync(u => (DateTime?)u.PasswordChangedAt, cancellationToken);
      items.Add(new AttentionItemDto("users.temporary-password", await temporary.CountAsync(cancellationToken), Utc(oldest)));
    }

    if (currentUser.HasPermission(PermissionCatalog.Users.Assign))
    {
      var noAccess = UserFilters.WithStatus(context, users, UserStatusFilter.NoAccess, now);
      var oldest = await noAccess.MinAsync(u => u.CreatedAt, cancellationToken);
      items.Add(new AttentionItemDto("users.no-access", await noAccess.CountAsync(cancellationToken), Utc(oldest)));

      // people, not grants: the count is what the Users register's "Temporary role ending" filter lists
      var soon = now.AddDays(7);
      var ending = context.UserRoles.AsNoTracking()
        .Where(ur => ur.RevokedAt == null && ur.ExpiresAt != null && ur.ExpiresAt > now && ur.ExpiresAt <= soon)
        .Join(users, ur => ur.UserId, u => u.Id, (ur, u) => ur);
      var first = await ending.MinAsync(ur => ur.ExpiresAt, cancellationToken);
      var people = await UserFilters.WithStatus(context, users, UserStatusFilter.RoleEnding, now).CountAsync(cancellationToken);
      items.Add(new AttentionItemDto("roles.ending", people, NextDue: first is { } day ? DateOnly.FromDateTime(day) : null));
    }

    if (currentUser.HasPermission(PermissionCatalog.Security.View))
    {
      var since = now.AddHours(-24);
      var failed = context.LoginAttempts.AsNoTracking().Where(a => !a.Succeeded && a.AttemptedAt >= since);
      var oldest = await failed.MinAsync(a => (DateTime?)a.AttemptedAt, cancellationToken);
      items.Add(new AttentionItemDto("signins.failed", await failed.CountAsync(cancellationToken), Utc(oldest)));
    }

    return Result<AttentionDto>.Success(new AttentionDto(DateTimeOffset.UtcNow, items));
  }

  private static DateTimeOffset? Utc(DateTime? value) =>
    value is { } v ? new DateTimeOffset(DateTime.SpecifyKind(v, DateTimeKind.Utc)) : null;
}
