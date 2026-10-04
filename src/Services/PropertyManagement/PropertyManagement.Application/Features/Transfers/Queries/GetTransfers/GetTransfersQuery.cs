using FluentValidation;

public sealed record GetTransfersQueryResult(PaginatedResult<TransferListItemDto> Transfers);

/// Transfers across all properties. From / To: the transfer date (inclusive). Search matches part of the
/// transfer no., its reference no., a party's code or name, or the property's code or name.
/// Default order: the latest transfer first.
public sealed record GetTransfersQuery(
  PaginationRequest Pagination,
  DateOnly? From = null,
  DateOnly? To = null,
  TransferStatus? Status = null,
  Guid? TransferTypeId = null,
  Guid? PropertyId = null,
  Guid? TownId = null,
  string? Search = null,
  TransferSort SortBy = TransferSort.Date,
  bool SortDescending = true) : IQuery<Result<GetTransfersQueryResult>>;

public class GetTransfersQueryValidator : AbstractValidator<GetTransfersQuery>
{
  public GetTransfersQueryValidator()
  {
    RuleFor(x => x.Pagination.Pageindex).GreaterThanOrEqualTo(0);
    RuleFor(x => x.Pagination.PageSize).InclusiveBetween(1, 5000);
    RuleFor(x => x.Status).IsInEnum().When(x => x.Status.HasValue);
    RuleFor(x => x.SortBy).IsInEnum();
    RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From).When(x => x.From.HasValue && x.To.HasValue)
      .WithMessage("'Transferred until' cannot be before 'Transferred from'.");
  }
}
