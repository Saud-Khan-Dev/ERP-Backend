public sealed record GetAdminActivityQueryResult(PaginatedResult<AdminActivityDto> Activities);

/// The administration activity trail: who changed what, filtered by actor, target, kind or date.
public sealed record GetAdminActivityQuery(
  PaginationRequest Pagination,
  Guid? ActorUserId = null,
  Guid? TargetId = null,
  string? Action = null,
  DateTime? From = null,
  DateTime? To = null) : IQuery<Result<GetAdminActivityQueryResult>>;
