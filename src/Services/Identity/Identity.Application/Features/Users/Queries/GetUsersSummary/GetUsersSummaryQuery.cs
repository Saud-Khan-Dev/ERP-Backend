using Microsoft.EntityFrameworkCore;

/// How many accounts are in each state the Users register filters by. Each figure comes from the same filter the
/// register applies (`UserFilters`), so a tile's number is exactly what its click lists.
public sealed record UsersSummaryDto(
  /// Every account that is not deleted.
  int Total,
  int Active,
  int Locked,
  int TemporaryPassword,
  int NeverSignedIn,
  int NoAccess,
  int Deactivated,
  int Deleted,
  int AuthorizedOfficers,
  /// Active accounts that signed in during the last seven days.
  int SignedInLast7Days);

public sealed record GetUsersSummaryQuery : IQuery<Result<UsersSummaryDto>>;

public class GetUsersSummaryHandler(IApplicationDbContext context) : IQueryHandler<GetUsersSummaryQuery, Result<UsersSummaryDto>>
{
  public async Task<Result<UsersSummaryDto>> Handle(GetUsersSummaryQuery query, CancellationToken cancellationToken)
  {
    var now = DateTime.UtcNow;
    var users = context.Users.AsNoTracking();
    var all = context.Users.IgnoreQueryFilters().AsNoTracking();

    Task<int> Count(string status) => UserFilters.WithStatus(context, status == UserStatusFilter.Deleted ? all : users, status, now).CountAsync(cancellationToken);

    // one DbContext: the counts run one after another
    var summary = new UsersSummaryDto(
      Total: await users.CountAsync(cancellationToken),
      Active: await Count(UserStatusFilter.Active),
      Locked: await Count(UserStatusFilter.Locked),
      TemporaryPassword: await Count(UserStatusFilter.TemporaryPassword),
      NeverSignedIn: await Count(UserStatusFilter.NeverSignedIn),
      NoAccess: await Count(UserStatusFilter.NoAccess),
      Deactivated: await Count(UserStatusFilter.Deactivated),
      Deleted: await Count(UserStatusFilter.Deleted),
      AuthorizedOfficers: await users.CountAsync(u => u.IsActive && u.IsAuthorizedOfficer, cancellationToken),
      SignedInLast7Days: await users.CountAsync(u => u.IsActive && u.LastLoginAt != null && u.LastLoginAt >= now.AddDays(-7), cancellationToken));

    return Result<UsersSummaryDto>.Success(summary);
  }
}
