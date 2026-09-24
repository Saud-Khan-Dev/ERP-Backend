public sealed record GetLoginAttemptsQueryResult(PaginatedResult<LoginAttemptDto> Attempts);

/// Security audit: who tried to sign in, from where, and whether it worked.
public sealed record GetLoginAttemptsQuery(
  PaginationRequest Pagination,
  Guid? UserId = null,
  string? Username = null,
  bool? Succeeded = null,
  DateTime? From = null,
  DateTime? To = null) : IQuery<Result<GetLoginAttemptsQueryResult>>;
