using FluentValidation;

public sealed record GetPropertiesQueryResult(PaginatedResult<PropertyListItemDto> Properties);

public sealed record GetPropertiesQuery(
  PaginationRequest Pagination,
  string? Search = null,
  Guid? TownId = null,
  Guid? PropertyTypeId = null,
  Guid? PropertyStatusId = null,
  Guid? PropertyClassificationId = null,
  bool IncludeInactive = false) : IQuery<Result<GetPropertiesQueryResult>>;

public class GetPropertiesQueryValidator : AbstractValidator<GetPropertiesQuery>
{
  public GetPropertiesQueryValidator()
  {
    RuleFor(x => x.Pagination.Pageindex).GreaterThanOrEqualTo(0);
    RuleFor(x => x.Pagination.PageSize).InclusiveBetween(1, 200);
  }
}
