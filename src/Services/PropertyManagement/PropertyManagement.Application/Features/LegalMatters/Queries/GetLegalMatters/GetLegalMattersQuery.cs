using FluentValidation;

public sealed record GetLegalMattersQueryResult(PaginatedResult<LegalMatterListItemDto> LegalMatters);

/// Encroachments, court cases, appeals and agreement violations across all properties, as one list.
/// Kinds: empty = all four. Open: see OpenMatters. Opened / next dates are the row's OpenedOn / NextDate.
/// Court: part of the court (cases) or appellate authority (appeals); encroachments and violations have none, so a
/// court filter leaves them out. Statuses: status codes as the rows return them (PENDING, UnderHearing, Open ...;
/// case and underscores ignored), any of them. PartyId: the encroacher, a party to the case, the appellant or the
/// violator. NextWithinDays: open with the next date between today and today + N days. HearingOverdue: true = open
/// with a next date (hearing, decision due, notice deadline) already past; false = not such a row.
/// Search matches part of the code, title, party (also by CNIC / NTN), court, or the property's code or name.
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
  bool SortDescending = false,
  string? Court = null,
  IReadOnlyList<string>? Statuses = null,
  Guid? PartyId = null,
  int? NextWithinDays = null,
  bool? HearingOverdue = null,
  bool? FineUnpaid = null) : IQuery<Result<GetLegalMattersQueryResult>>;

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
    RuleFor(x => x.Court).MaximumLength(200);
    RuleFor(x => x.NextWithinDays).InclusiveBetween(0, 3650).When(x => x.NextWithinDays.HasValue)
      .WithMessage("'Next date within' must be between 0 and 3650 days.");
  }
}
