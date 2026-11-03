using FluentValidation;
using Microsoft.EntityFrameworkCore;

/// A sign-in account as any module may show it: who did something ("Prepared by Farukh Jadoon").
public sealed record DirectoryUserDto(Guid Id, string DisplayName, string Username, bool IsActive);

public sealed record GetDirectoryUsersQueryResult(IReadOnlyList<DirectoryUserDto> Users);

/// Names for the user ids other services store on their records (prepared by, approved by, decided by) - read by any
/// signed-in person, so it carries a name and nothing else. Deleted accounts are still named: history must read.
/// `Permission`: the accounts in use that hold that permission now - who can act on something ("approved by any of ...").
public sealed record GetDirectoryUsersQuery(IReadOnlyList<Guid> Ids, string? Search, string? Permission = null) : IQuery<Result<GetDirectoryUsersQueryResult>>;

public class GetDirectoryUsersQueryValidator : AbstractValidator<GetDirectoryUsersQuery>
{
  public GetDirectoryUsersQueryValidator()
  {
    RuleFor(x => x.Ids).Must(ids => ids.Count <= 200).WithMessage("Ask for at most 200 accounts at a time.");
    RuleFor(x => x).Must(x => x.Ids.Count > 0 || !string.IsNullOrWhiteSpace(x.Search) || !string.IsNullOrWhiteSpace(x.Permission))
      .WithMessage("Ask for accounts by id, search by name, or name a permission.");
    RuleFor(x => x.Permission).MaximumLength(100);
    RuleFor(x => x.Search).MaximumLength(100);
  }
}

public class GetDirectoryUsersHandler(IApplicationDbContext context) : IQueryHandler<GetDirectoryUsersQuery, Result<GetDirectoryUsersQueryResult>>
{
  public async Task<Result<GetDirectoryUsersQueryResult>> Handle(GetDirectoryUsersQuery query, CancellationToken cancellationToken)
  {
    if (!string.IsNullOrWhiteSpace(query.Permission))
    {
      var code = query.Permission.Trim();
      var map = await AccessMap.BuildAsync(context, cancellationToken);
      var holders = map.Accounts.Values
        .Where(a => a.User.IsActive && a.User.DeletedAt == null && a.Permissions.ContainsKey(code))
        .Select(a => new DirectoryUserDto(a.User.Id.Value, a.User.DisplayName.Value, a.User.Username.Value, true))
        .OrderBy(u => u.DisplayName).Take(200).ToList();
      return Result<GetDirectoryUsersQueryResult>.Success(new GetDirectoryUsersQueryResult(holders));
    }

    var users = context.Users.IgnoreQueryFilters().AsNoTracking();
    if (query.Ids.Count > 0)
    {
      var ids = query.Ids.Distinct().Select(UserId.Of).ToList();
      users = users.Where(u => ids.Contains(u.Id));
    }
    else
    {
      var pattern = $"%{query.Search!.Trim().ToLowerInvariant().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%";
      users = users.Where(u => u.DeletedAt == null && EF.Functions.Like(EF.Property<string>(u, UserSearch.Property), pattern, "\\"));
    }

    var rows = await users.OrderBy(u => u.DisplayName.Value).Take(200)
      .Select(u => new DirectoryUserDto(u.Id.Value, u.DisplayName.Value, u.Username.Value, u.IsActive && u.DeletedAt == null))
      .ToListAsync(cancellationToken);
    return Result<GetDirectoryUsersQueryResult>.Success(new GetDirectoryUsersQueryResult(rows));
  }
}
