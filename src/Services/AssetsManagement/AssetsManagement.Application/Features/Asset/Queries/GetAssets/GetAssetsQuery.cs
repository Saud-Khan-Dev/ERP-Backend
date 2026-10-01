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

/// Disposed = an asset with a disposal record. Include (default) lists both, Exclude hides them, Only lists just them.
public enum DisposalFilter
{
  Include,
  Exclude,
  Only
}

public enum AssetSort
{
  Code,
  Name,
  Registered,
  Acquired,
  Cost
}

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
  string? Search = null,
  bool IncludeInactive = false,
  IReadOnlyList<AttributeFilter>? AttributeFilters = null,
  bool IncludeSubLocations = true,
  OwnershipType? Ownership = null,
  DisposalFilter Disposed = DisposalFilter.Include,
  DateTime? RegisteredFrom = null,
  DateTime? RegisteredTo = null,
  DateOnly? AcquiredFrom = null,
  DateOnly? AcquiredTo = null,
  decimal? CostMin = null,
  decimal? CostMax = null,
  AssetSort SortBy = AssetSort.Code,
  bool SortDescending = false) : IQuery<Result<GetAssetsQueryResult>>;

public class GetAssetsQueryValidator : AbstractValidator<GetAssetsQuery>
{
  public GetAssetsQueryValidator()
  {
    RuleFor(x => x.Pagination.Pageindex).GreaterThanOrEqualTo(0);
    RuleFor(x => x.Pagination.PageSize).InclusiveBetween(1, 200);
    RuleFor(x => x.RegisteredTo).GreaterThanOrEqualTo(x => x.RegisteredFrom).When(x => x.RegisteredFrom.HasValue && x.RegisteredTo.HasValue)
      .WithMessage("'Registered until' cannot be before 'Registered from'.");
    RuleFor(x => x.AcquiredTo).GreaterThanOrEqualTo(x => x.AcquiredFrom).When(x => x.AcquiredFrom.HasValue && x.AcquiredTo.HasValue)
      .WithMessage("'Purchased until' cannot be before 'Purchased from'.");
    RuleFor(x => x.CostMax).GreaterThanOrEqualTo(x => x.CostMin).When(x => x.CostMin.HasValue && x.CostMax.HasValue)
      .WithMessage("The highest cost cannot be below the lowest cost.");
    RuleFor(x => x.Ownership).IsInEnum().When(x => x.Ownership.HasValue);
    RuleForEach(x => x.AttributeFilters).ChildRules(f =>
    {
      f.RuleFor(v => v.Code).NotEmpty();
      f.RuleFor(v => v.Operator).IsInEnum();
      f.RuleFor(v => v.Value).NotNull();
    });
  }
}
