using FluentValidation;

public sealed record GetUsersQueryResult(PaginatedResult<UserListItemDto> Users);

/// The Users register. `Status` is one of `UserStatusFilter.All`; `Sort` one of name, username, lastSignIn, registered.
public sealed record GetUsersQuery(
  PaginationRequest Pagination,
  string? Search = null,
  Guid? RoleId = null,
  bool? IsActive = null,
  bool IncludeDeleted = false,
  string? Status = null,
  string? Sort = null,
  string? Direction = null) : IQuery<Result<GetUsersQueryResult>>;

/// The shadow property the search matches (a generated column: username, email, employee code and name, lower case).
public static class UserSearch
{
  public const string Property = "SearchText";
}

/// The states the Users register filters by - each one a question an administrator asks.
public static class UserStatusFilter
{
  /// Can sign in (not switched off; a locked account is still active).
  public const string Active = "active";
  /// Locked after too many wrong passwords, now.
  public const string Locked = "locked";
  /// Still holds the temporary password an administrator gave it.
  public const string TemporaryPassword = "temporary-password";
  /// Active, and has never signed in.
  public const string NeverSignedIn = "never-signed-in";
  /// Active, but no live role of a switched-on role and no live Allow exception: can open nothing.
  public const string NoAccess = "no-access";
  /// Switched off by an administrator.
  public const string Deactivated = "deactivated";
  /// Deleted (kept for the audit trail; can be restored).
  public const string Deleted = "deleted";
  /// Holds a temporary role that ends within seven days.
  public const string RoleEnding = "role-ending";

  public static readonly IReadOnlyList<string> All = [Active, Locked, TemporaryPassword, NeverSignedIn, NoAccess, Deactivated, Deleted, RoleEnding];
}

public class GetUsersQueryValidator : AbstractValidator<GetUsersQuery>
{
  public GetUsersQueryValidator()
  {
    RuleFor(x => x.Pagination.Pageindex).GreaterThanOrEqualTo(0).WithMessage("The page number cannot be negative.");
    RuleFor(x => x.Pagination.PageSize).InclusiveBetween(1, 200).WithMessage("Ask for between 1 and 200 accounts at a time.");
    RuleFor(x => x.Status).Must(s => s is null || UserStatusFilter.All.Contains(s))
      .WithMessage($"Status must be one of: {string.Join(", ", UserStatusFilter.All)}.");
    RuleFor(x => x.Sort).Must(s => s is null || s is "name" or "username" or "lastSignIn" or "registered")
      .WithMessage("Sort by name, username, lastSignIn or registered.");
    RuleFor(x => x.Direction).Must(d => d is null || d is "asc" or "desc").WithMessage("Direction is asc or desc.");
    RuleFor(x => x.Search).MaximumLength(100);
  }
}
