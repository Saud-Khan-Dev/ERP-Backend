using Microsoft.EntityFrameworkCore;

public class GetUsersHandler(IApplicationDbContext context)
  : IQueryHandler<GetUsersQuery, Result<GetUsersQueryResult>>
{
  public async Task<Result<GetUsersQueryResult>> Handle(GetUsersQuery query, CancellationToken cancellationToken)
  {
    var now = DateTime.UtcNow;
    var deleted = query.Status == UserStatusFilter.Deleted;

    var users = query.IncludeDeleted || deleted
      ? context.Users.IgnoreQueryFilters().AsNoTracking()
      : context.Users.AsNoTracking();

    users = UserFilters.WithStatus(context, users, query.Status, now);

    if (query.IsActive.HasValue)
      users = users.Where(u => u.IsActive == query.IsActive.Value);

    if (query.RoleId.HasValue)
    {
      // the same liveness as the Roles column and the role's holder count: not removed, not run out
      var roleId = RoleId.Of(query.RoleId.Value);
      users = users.Where(u => context.UserRoles
          .Any(ur => ur.UserId == u.Id && ur.RoleId == roleId && ur.RevokedAt == null && (ur.ExpiresAt == null || ur.ExpiresAt > now)));
    }

    if (!string.IsNullOrWhiteSpace(query.Search))
    {
      // part of the username, email, employee code or name; Like + ToLower keeps the Application layer provider-agnostic
      var pattern = $"%{EscapeLike(query.Search.Trim().ToLowerInvariant())}%";
      users = users.Where(u => EF.Functions.Like(EF.Property<string>(u, UserSearch.Property), pattern, "\\"));
    }

    var totalCount = await users.LongCountAsync(cancellationToken);

    var descending = query.Direction == "desc";
    var ordered = query.Sort switch
    {
      "username" => descending ? users.OrderByDescending(u => u.Username) : users.OrderBy(u => u.Username),
      // never signed in sorts last whichever way
      "lastSignIn" => descending
        ? users.OrderBy(u => u.LastLoginAt == null).ThenByDescending(u => u.LastLoginAt)
        : users.OrderBy(u => u.LastLoginAt == null).ThenBy(u => u.LastLoginAt),
      "registered" => descending ? users.OrderByDescending(u => u.CreatedAt) : users.OrderBy(u => u.CreatedAt),
      _ => descending ? users.OrderByDescending(u => u.DisplayName.Value) : users.OrderBy(u => u.DisplayName.Value),
    };

    var page = await ordered
        .ThenBy(u => u.Username)
        .Skip(query.Pagination.Pageindex * query.Pagination.PageSize)
        .Take(query.Pagination.PageSize)
        .ToListAsync(cancellationToken);

    // one round trip for the roles of everyone on this page, rather than one per row
    var userIds = page.Select(u => u.Id).ToList();

    var grants = (await context.UserRoles.AsNoTracking()
        .Where(ur => userIds.Contains(ur.UserId) && ur.RevokedAt == null)
        .Join(context.Roles.AsNoTracking(), ur => ur.RoleId, r => r.Id,
            (ur, r) => new { ur.UserId, ur.ExpiresAt, r.Code, Name = r.RoleName, r.IsActive })
        .ToListAsync(cancellationToken))
        .Where(x => x.ExpiresAt == null || x.ExpiresAt > now)
        .ToList();
    var roleCodes = grants.GroupBy(x => x.UserId)
        .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(x => x.Code.Value).OrderBy(c => c).ToList());
    var roleNames = grants.GroupBy(x => x.UserId)
        .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(x => x.IsActive ? x.Name.Value : $"{x.Name.Value} (switched off)").OrderBy(n => n).ToList());

    // "can open nothing": no live grant of a switched-on role and no live Allow exception
    var withAccess = (await UserFilters.WithAccess(context, now).Where(id => userIds.Contains(id)).ToListAsync(cancellationToken)).ToHashSet();

    var data = page
        .Select(u => u.ToListItemDto(now,
          roleCodes.GetValueOrDefault(u.Id, Array.Empty<string>()),
          roleNames.GetValueOrDefault(u.Id, Array.Empty<string>()),
          withAccess.Contains(u.Id)))
        .ToList();

    return Result<GetUsersQueryResult>.Success(new GetUsersQueryResult(
      new PaginatedResult<UserListItemDto>(query.Pagination.Pageindex, query.Pagination.PageSize, totalCount, data)));
  }

  private static string EscapeLike(string term) => term.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}

/// The register's status filters, shared by the list and its summary so a tile's count is what its click shows.
public static class UserFilters
{
  public static IQueryable<User> WithStatus(IApplicationDbContext context, IQueryable<User> users, string? status, DateTime now) => status switch
  {
    UserStatusFilter.Active => users.Where(u => u.IsActive),
    UserStatusFilter.Locked => users.Where(u => u.LockedUntil != null && u.LockedUntil > now),
    UserStatusFilter.TemporaryPassword => users.Where(u => u.IsActive && u.MustChangePassword),
    UserStatusFilter.NeverSignedIn => users.Where(u => u.IsActive && u.LastLoginAt == null),
    UserStatusFilter.NoAccess => users.Where(u => u.IsActive && !WithAccess(context, now).Contains(u.Id)),
    UserStatusFilter.Deactivated => users.Where(u => !u.IsActive && u.DeletedAt == null),
    UserStatusFilter.Deleted => users.Where(u => u.DeletedAt != null),
    UserStatusFilter.RoleEnding => users.Where(u => context.UserRoles.Any(ur => ur.UserId == u.Id && ur.RevokedAt == null
      && ur.ExpiresAt != null && ur.ExpiresAt > now && ur.ExpiresAt <= now.AddDays(7))),
    _ => users,
  };

  /// The accounts that can open something: a live grant of a switched-on role, or a live Allow exception.
  public static IQueryable<UserId> WithAccess(IApplicationDbContext context, DateTime now) =>
    context.UserRoles
      .Where(ur => ur.RevokedAt == null && (ur.ExpiresAt == null || ur.ExpiresAt > now)
        && context.Roles.Any(r => r.Id == ur.RoleId && r.IsActive))
      .Select(ur => ur.UserId)
      .Union(context.UserPermissionOverrides
        .Where(o => o.Effect == OverrideEffect.Allow && (o.ExpiresAt == null || o.ExpiresAt > now))
        .Select(o => o.UserId));
}
