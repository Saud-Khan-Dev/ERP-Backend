using FluentValidation;

public sealed record GetPropertiesQueryResult(PaginatedResult<PropertyListItemDto> Properties);

/// type: property type name; owners: how many hold a current share; holder: the name the "Held by" column shows (the
/// first holder by name, else the allottee in force), properties nobody holds last.
public enum PropertySort
{
  Code,
  Name,
  Registered,
  Area,
  Town,
  Status,
  Type,
  Owners,
  Holder
}

/// RegisteredFrom / RegisteredTo: instants on the audit created_at (both inclusive).
/// AreaMin / AreaMax: current total area in square feet (the current measurement's base value).
/// OwnerId: properties where that owner holds a current share (ACTIVE or DISPUTED).
/// Search: part of the code, name, khasra number or address, or a party holding it now - a current holder, the allottee,
/// lessee, tenant or contractor in force - by part of the name or a CNIC (the first 5 or more digits, any written form).
/// TransferPending: a transfer waiting for approval or registration. Allotted: an allotment in force today.
/// AgreementInForce: an allotment, lease, rental or contract in force today. HasHolder: anyone holds a current share.
/// Charged: a charge (active or enforced) on it. Disputed: a disputed share. Measured: a current measurement.
/// HasBoundary: a current boundary survey.
/// HasOpenEncroachment / HasOpenCase: true = at least one unresolved encroachment / pending court case,
/// false = none (see PropertyQueryFilter for what counts as open).
/// SteepSlope: true = the current boundary survey's slope is above the 45 degree BCR limit, false = it is not (or none).
public sealed record GetPropertiesQuery(
  PaginationRequest Pagination,
  string? Search = null,
  Guid? TownId = null,
  Guid? PropertyTypeId = null,
  Guid? PropertyStatusId = null,
  Guid? PropertyClassificationId = null,
  bool IncludeInactive = false,
  DateTime? RegisteredFrom = null,
  DateTime? RegisteredTo = null,
  decimal? AreaMin = null,
  decimal? AreaMax = null,
  Guid? OwnerId = null,
  bool? HasOpenEncroachment = null,
  bool? HasOpenCase = null,
  PropertySort SortBy = PropertySort.Code,
  bool SortDescending = false,
  bool? SteepSlope = null,
  bool? TransferPending = null,
  bool? Allotted = null,
  bool? AgreementInForce = null,
  bool? HasHolder = null,
  bool? Charged = null,
  bool? Disputed = null,
  bool? Measured = null,
  bool? HasBoundary = null) : IQuery<Result<GetPropertiesQueryResult>>;

public class GetPropertiesQueryValidator : AbstractValidator<GetPropertiesQuery>
{
  public GetPropertiesQueryValidator()
  {
    RuleFor(x => x.Pagination.Pageindex).GreaterThanOrEqualTo(0);
    RuleFor(x => x.Pagination.PageSize).InclusiveBetween(1, 200);
    Include(new PropertyFilterRules());
  }
}

/// The filter rules shared by the register and the register report. Internal so assembly scanning does not register it
/// as a validator of its own (the errors would be reported twice).
internal class PropertyFilterRules : AbstractValidator<GetPropertiesQuery>
{
  public PropertyFilterRules()
  {
    RuleFor(x => x.RegisteredTo).GreaterThanOrEqualTo(x => x.RegisteredFrom).When(x => x.RegisteredFrom.HasValue && x.RegisteredTo.HasValue)
      .WithMessage("'Registered until' cannot be before 'Registered from'.");
    RuleFor(x => x.AreaMin).GreaterThanOrEqualTo(0).When(x => x.AreaMin.HasValue);
    RuleFor(x => x.AreaMax).GreaterThanOrEqualTo(x => x.AreaMin).When(x => x.AreaMin.HasValue && x.AreaMax.HasValue)
      .WithMessage("The largest area cannot be below the smallest area.");
    RuleFor(x => x.SortBy).IsInEnum();
  }
}
