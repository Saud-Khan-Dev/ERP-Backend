using FluentValidation;

public enum AttributeFilterOperator
{
  Eq,
  Neq,
  Gt,
  Gte,
  Lt,
  Lte,
  Contains
}

/// Filters on a dynamic attribute through the typed projection (asset_attribute_value), e.g. ram_gb gte 16.
public sealed record AttributeFilter(string Code, AttributeFilterOperator Operator, string Value);

public sealed record GetAssetsQueryResult(PaginatedResult<AssetListItemDto> Assets);

public sealed record GetAssetsQuery(
  PaginationRequest Pagination,
  Guid? AssetClassId = null,
  Guid? AssetTypeId = null,
  Guid? CategoryId = null,
  bool IncludeSubCategories = true,
  Guid? StatusId = null,
  Guid? LocationId = null,
  Guid? CustodianId = null,
  Guid? DepartmentId = null,
  Guid? ParentAssetId = null,
  string? Search = null,
  bool IncludeInactive = false,
  IReadOnlyList<AttributeFilter>? AttributeFilters = null) : IQuery<Result<GetAssetsQueryResult>>;

public class GetAssetsQueryValidator : AbstractValidator<GetAssetsQuery>
{
  public GetAssetsQueryValidator()
  {
    RuleFor(x => x.Pagination.Pageindex).GreaterThanOrEqualTo(0);
    RuleFor(x => x.Pagination.PageSize).InclusiveBetween(1, 200);
    RuleForEach(x => x.AttributeFilters).ChildRules(f =>
    {
      f.RuleFor(v => v.Code).NotEmpty();
      f.RuleFor(v => v.Operator).IsInEnum();
      f.RuleFor(v => v.Value).NotNull();
    });
  }
}
