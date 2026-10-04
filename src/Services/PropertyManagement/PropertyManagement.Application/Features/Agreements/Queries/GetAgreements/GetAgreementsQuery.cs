using FluentValidation;

public sealed record GetAgreementsQueryResult(PaginatedResult<AgreementListItemDto> Agreements);

/// Allotments, leases, rentals, outsourcing contracts and auctions across all properties, as one list.
/// Kinds: empty = all five. PartyId: the allottee / lessee / tenant / contractor / successful bidder.
/// Start / end dates are the row's StartDate / EndDate (see GetAgreementsHandler). Search matches part of the
/// record's code, the party's code or name, or the property's code or name.
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
  bool SortDescending = false) : IQuery<Result<GetAgreementsQueryResult>>;

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
  }
}
