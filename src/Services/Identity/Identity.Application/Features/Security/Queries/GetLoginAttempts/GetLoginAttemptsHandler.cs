using Microsoft.EntityFrameworkCore;

public class GetLoginAttemptsHandler(IApplicationDbContext context)
  : IQueryHandler<GetLoginAttemptsQuery, Result<GetLoginAttemptsQueryResult>>
{
  public async Task<Result<GetLoginAttemptsQueryResult>> Handle(GetLoginAttemptsQuery query, CancellationToken cancellationToken)
  {
    var attempts = context.LoginAttempts.AsNoTracking();

    if (query.UserId.HasValue)
    {
      var userId = UserId.Of(query.UserId.Value);
      attempts = attempts.Where(a => a.UserId == userId);
    }

    if (!string.IsNullOrWhiteSpace(query.Username))
    {
      var username = query.Username.Trim().ToLowerInvariant();
      attempts = attempts.Where(a => a.AttemptedUsername.Contains(username));
    }

    if (query.Succeeded.HasValue)
      attempts = attempts.Where(a => a.Succeeded == query.Succeeded.Value);

    if (!string.IsNullOrWhiteSpace(query.Reason) && SignInFailureReasons.Texts.TryGetValue(query.Reason, out var texts))
      attempts = attempts.Where(a => !a.Succeeded && a.FailureReason != null && texts.Contains(a.FailureReason));

    // a date without a zone is read as UTC (the screens send UTC)
    if (query.From is { } from)
    {
      var since = AsUtc(from);
      attempts = attempts.Where(a => a.AttemptedAt >= since);
    }

    if (query.To is { } to)
    {
      var until = AsUtc(to);
      attempts = attempts.Where(a => a.AttemptedAt <= until);
    }

    var totalCount = await attempts.LongCountAsync(cancellationToken);

    var page = await attempts
        .OrderByDescending(a => a.AttemptedAt)
        .Skip(query.Pagination.Pageindex * query.Pagination.PageSize)
        .Take(query.Pagination.PageSize)
        .ToListAsync(cancellationToken);

    return Result<GetLoginAttemptsQueryResult>.Success(new GetLoginAttemptsQueryResult(
      new PaginatedResult<LoginAttemptDto>(
        query.Pagination.Pageindex, query.Pagination.PageSize, totalCount,
        page.Select(a => a.ToDto()).ToList())));
  }

  private static DateTime AsUtc(DateTime value) =>
    value.Kind == DateTimeKind.Utc ? value : value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
