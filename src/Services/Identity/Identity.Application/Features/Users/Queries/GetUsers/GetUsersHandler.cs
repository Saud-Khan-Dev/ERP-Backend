using Microsoft.EntityFrameworkCore;

public class GetUsersHandler(IApplicationDbContext context)
  : IQueryHandler<GetUsersQuery, Result<GetUsersQueryResult>>
{
  public async Task<Result<GetUsersQueryResult>> Handle(GetUsersQuery query, CancellationToken cancellationToken)
  {
    var now = DateTime.UtcNow;

    var users = query.IncludeDeleted
      ? context.Users.IgnoreQueryFilters().AsNoTracking()
      : context.Users.AsNoTracking();

    if (query.IsActive.HasValue)
      users = users.Where(u => u.IsActive == query.IsActive.Value);

    if (query.RoleId.HasValue)
    {
      var roleId = RoleId.Of(query.RoleId.Value);
      users = users.Where(u => context.UserRoles
          .Any(ur => ur.UserId == u.Id && ur.RoleId == roleId && ur.RevokedAt == null));
    }

    if (!string.IsNullOrWhiteSpace(query.Search))
    {
      var term = query.Search.Trim();

      // username and email are value-converted identifiers: they match exactly, and an
      // unparseable term simply matches neither. Display name is a complex property, so a
      // case-insensitive partial match translates to SQL.
      Username? username = null;
      EmailAddress? email = null;
      try { username = Username.Of(term); } catch (DomainException) { }
      try { email = EmailAddress.Of(term); } catch (DomainException) { }

      // Like + ToLower rather than Npgsql's ILike: the Application layer stays provider-agnostic.
      var pattern = $"%{term.ToLowerInvariant()}%";

      users = users.Where(u =>
          (username != null && u.Username == username)
          || (email != null && u.Email == email)
          || EF.Functions.Like(u.DisplayName.Value.ToLower(), pattern));
    }

    var totalCount = await users.LongCountAsync(cancellationToken);

    var page = await users
        .OrderBy(u => u.Username)
        .Skip(query.Pagination.Pageindex * query.Pagination.PageSize)
        .Take(query.Pagination.PageSize)
        .ToListAsync(cancellationToken);

    // one round trip for the roles of everyone on this page, rather than one per row
    var userIds = page.Select(u => u.Id).ToList();

    var roleLookup = (await context.UserRoles.AsNoTracking()
        .Where(ur => userIds.Contains(ur.UserId) && ur.RevokedAt == null)
        .Join(context.Roles.AsNoTracking(), ur => ur.RoleId, r => r.Id,
            (ur, r) => new { ur.UserId, ur.ExpiresAt, r.Code })
        .ToListAsync(cancellationToken))
        .Where(x => x.ExpiresAt == null || x.ExpiresAt > now)
        .GroupBy(x => x.UserId)
        .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(x => x.Code.Value).OrderBy(c => c).ToList());

    var data = page
        .Select(u => u.ToListItemDto(now, roleLookup.GetValueOrDefault(u.Id, Array.Empty<string>())))
        .ToList();

    return Result<GetUsersQueryResult>.Success(new GetUsersQueryResult(
      new PaginatedResult<UserListItemDto>(query.Pagination.Pageindex, query.Pagination.PageSize, totalCount, data)));
  }
}
