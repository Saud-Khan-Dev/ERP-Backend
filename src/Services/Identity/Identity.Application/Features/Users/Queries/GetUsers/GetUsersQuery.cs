public sealed record GetUsersQueryResult(PaginatedResult<UserListItemDto> Users);

public sealed record GetUsersQuery(
  PaginationRequest Pagination,
  string? Search = null,
  Guid? RoleId = null,
  bool? IsActive = null,
  bool IncludeDeleted = false) : IQuery<Result<GetUsersQueryResult>>;
