using FluentValidation;

public sealed record GetAgreementsQueryResult(PaginatedResult<AgreementListItemDto> Agreements);

/// Allotments, leases, rentals, outsourcing contracts and auctions across all properties, as one list.
/// Kinds: empty = all five. PartyId: the allottee / lessee / tenant / contractor / successful bidder.
/// InForce: see InForce. Statuses: status codes (ACTIVE, DRAFT, PLANNED ...), any of them; empty = all.
/// TypeId: one allotment / lease / rental / outsourcing / auction type. AmountMin / AmountMax: on the annual
/// equivalent (AnnualAmount; rows without an amount are left out once either is given).
/// EndsWithinDays: in force and ending between today and today + N days. Overdue: true = in force although the end
/// date has passed, false = not such a row.
/// Start / end dates are the row's StartDate / EndDate (see GetAgreementsHandler). Search matches part of the
/// record's code, the party's code or name, the party's CNIC or NTN, or the property's code or name.
/// Default order: the soonest end first, rows without an end date last.
public sealed record GetAgreementsQuery(
  PaginationRequest Pagination,
  IReadOnlyList<AgreementKind>? Kinds = null,
  Guid? PropertyId = null,
  Guid? TownId = null,
  Guid? PartyId = null,
  bool? InForce = null,
  DateOnly? StartFrom = null,
  DateOnly? StartTo = null,
  DateOnly? EndFrom = null,
  DateOnly? EndTo = null,
  string? Search = null,
  AgreementSort SortBy = AgreementSort.Ends,
  bool SortDescending = false,
  IReadOnlyList<string>? Statuses = null,
  Guid? TypeId = null,
  decimal? AmountMin = null,
  decimal? AmountMax = null,
  int? EndsWithinDays = null,
  bool? Overdue = null) : IQuery<Result<GetAgreementsQueryResult>>;

public class GetAgreementsQueryValidator : AbstractValidator<GetAgreementsQuery>
{
  public GetAgreementsQueryValidator()
  {
    RuleFor(x => x.Pagination.Pageindex).GreaterThanOrEqualTo(0);
    RuleFor(x => x.Pagination.PageSize).InclusiveBetween(1, 5000);
    RuleForEach(x => x.Kinds).IsInEnum();
    RuleFor(x => x.SortBy).IsInEnum();
    RuleFor(x => x.StartTo).GreaterThanOrEqualTo(x => x.StartFrom).When(x => x.StartFrom.HasValue && x.StartTo.HasValue)
      .WithMessage("'Starts until' cannot be before 'Starts from'.");
    RuleFor(x => x.EndTo).GreaterThanOrEqualTo(x => x.EndFrom).When(x => x.EndFrom.HasValue && x.EndTo.HasValue)
      .WithMessage("'Ends until' cannot be before 'Ends from'.");
    RuleFor(x => x.AmountMin).GreaterThanOrEqualTo(0).When(x => x.AmountMin.HasValue)
      .WithMessage("The smallest amount cannot be negative.");
    RuleFor(x => x.AmountMax).GreaterThanOrEqualTo(x => x.AmountMin).When(x => x.AmountMin.HasValue && x.AmountMax.HasValue)
      .WithMessage("The largest amount cannot be below the smallest amount.");
    RuleFor(x => x.EndsWithinDays).InclusiveBetween(0, 3650).When(x => x.EndsWithinDays.HasValue)
      .WithMessage("'Ends within' must be between 0 and 3650 days.");
  }
}
