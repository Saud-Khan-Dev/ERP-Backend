using FluentValidation;

public sealed record GetLegalMattersQueryResult(PaginatedResult<LegalMatterListItemDto> LegalMatters);

/// Encroachments, court cases, appeals and agreement violations across all properties, as one list.
/// Kinds: empty = all four. Open: see OpenMatters. Opened / next dates are the row's OpenedOn / NextDate.
/// Search matches part of the code, title, party, court, or the property's code or name.
/// Default order: the soonest next date first, rows without one last.
public sealed record GetLegalMattersQuery(
  PaginationRequest Pagination,
  IReadOnlyList<LegalMatterKind>? Kinds = null,
  Guid? PropertyId = null,
  Guid? TownId = null,
  bool? Open = null,
  DateOnly? OpenedFrom = null,
  DateOnly? OpenedTo = null,
  DateOnly? NextFrom = null,
  DateOnly? NextTo = null,
  string? Search = null,
  LegalMatterSort SortBy = LegalMatterSort.Next,
  bool SortDescending = false) : IQuery<Result<GetLegalMattersQueryResult>>;

public class GetLegalMattersQueryValidator : AbstractValidator<GetLegalMattersQuery>
{
  public GetLegalMattersQueryValidator()
  {
    RuleFor(x => x.Pagination.Pageindex).GreaterThanOrEqualTo(0);
    RuleFor(x => x.Pagination.PageSize).InclusiveBetween(1, 5000);
    RuleForEach(x => x.Kinds).IsInEnum();
    RuleFor(x => x.SortBy).IsInEnum();
    RuleFor(x => x.OpenedTo).GreaterThanOrEqualTo(x => x.OpenedFrom).When(x => x.OpenedFrom.HasValue && x.OpenedTo.HasValue)
      .WithMessage("'Opened until' cannot be before 'Opened from'.");
    RuleFor(x => x.NextTo).GreaterThanOrEqualTo(x => x.NextFrom).When(x => x.NextFrom.HasValue && x.NextTo.HasValue)
      .WithMessage("'Next date until' cannot be before 'Next date from'.");
  }
}
