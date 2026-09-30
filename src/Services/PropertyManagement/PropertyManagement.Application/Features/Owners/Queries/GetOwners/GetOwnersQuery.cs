using FluentValidation;

public sealed record GetOwnersQueryResult(PaginatedResult<OwnerListItemDto> Owners);

/// Search matches owner code or CNIC exactly and the name partially; cnic / ntn filter exactly —
/// the lookup to run before registering someone new.
public sealed record GetOwnersQuery(
  PaginationRequest Pagination,
  string? Search = null,
  string? Cnic = null,
  string? Ntn = null,
  Guid? OwnerTypeId = null,
  bool IncludeInactive = false) : IQuery<Result<GetOwnersQueryResult>>;

public class GetOwnersQueryValidator : AbstractValidator<GetOwnersQuery>
{
  public GetOwnersQueryValidator()
  {
    RuleFor(x => x.Pagination.Pageindex).GreaterThanOrEqualTo(0);
    RuleFor(x => x.Pagination.PageSize).InclusiveBetween(1, 200);
  }
}
