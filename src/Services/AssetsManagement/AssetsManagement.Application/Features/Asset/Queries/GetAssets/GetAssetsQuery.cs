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

/// Disposed = an asset with an approved disposal. Include (default) lists both, Exclude hides them, Only lists just them;
/// Proposed lists the assets whose disposal waits for approval (they are not disposed yet).
public enum DisposalFilter
{
  Include,
  Exclude,
  Only,
  Proposed
}

/// Out = on a loan that has not come back; Overdue = and its return date has passed.
public enum LoanFilter
{
  Out,
  Overdue
}

public enum AssetSort
{
  Code,
  Name,
  Registered,
  Acquired,
  Cost,
  Status,
  Location,
  Warranty
}

/// The register's filters. Money filters and sorts (cost, purchase date, with or without a purchase) and the money
/// columns need ASSET_FINANCE.VIEW - IncludeMoney says whether the caller holds it (the endpoint sets it).
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
  bool SortDescending = false,
  bool IncludeMoney = true,
  bool NoLocation = false,
  LoanFilter? Loan = null,
  DateOnly? WarrantyFrom = null,
  DateOnly? WarrantyTo = null,
  DateOnly? DisposedFrom = null,
  DateOnly? DisposedTo = null,
  bool? HasPurchase = null,
  bool DepreciationBehind = false,
  DateTime? StatusSinceTo = null,
  bool IncludeSubUnits = true,
  bool MissingRequiredLocation = false,
  DateOnly? HeldOn = null,
  bool OutOfService = false,
  int? InStatusOverDays = null,
  int? WarrantyWithinDays = null,
  bool? HasCustodian = null) : IQuery<Result<GetAssetsQueryResult>>
{
  /// The filters that read money (refused without ASSET_FINANCE.VIEW).
  public bool UsesMoney =>
      AcquiredFrom.HasValue || AcquiredTo.HasValue || CostMin.HasValue || CostMax.HasValue || HasPurchase.HasValue || DepreciationBehind
      || SortBy is AssetSort.Acquired or AssetSort.Cost;
}

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
    RuleFor(x => x.WarrantyTo).GreaterThanOrEqualTo(x => x.WarrantyFrom).When(x => x.WarrantyFrom.HasValue && x.WarrantyTo.HasValue)
      .WithMessage("'Warranty ends by' cannot be before 'Warranty ends from'.");
    RuleFor(x => x.DisposedTo).GreaterThanOrEqualTo(x => x.DisposedFrom).When(x => x.DisposedFrom.HasValue && x.DisposedTo.HasValue)
      .WithMessage("'Disposed until' cannot be before 'Disposed from'.");
    RuleFor(x => x.Ownership).IsInEnum().When(x => x.Ownership.HasValue);
    RuleFor(x => x.Search).MaximumLength(100);
    RuleFor(x => x.InStatusOverDays).InclusiveBetween(0, 3650).When(x => x.InStatusOverDays.HasValue);
    RuleFor(x => x.WarrantyWithinDays).InclusiveBetween(0, 3650).When(x => x.WarrantyWithinDays.HasValue);
    RuleFor(x => x).Must(x => !(x.NoLocation && x.LocationId.HasValue)).WithMessage("Choose a location or 'no location', not both.");
    RuleForEach(x => x.AttributeFilters).ChildRules(f =>
    {
      f.RuleFor(v => v.Code).NotEmpty();
      f.RuleFor(v => v.Operator).IsInEnum();
      f.RuleFor(v => v.Value).NotNull();
    });
  }
}
