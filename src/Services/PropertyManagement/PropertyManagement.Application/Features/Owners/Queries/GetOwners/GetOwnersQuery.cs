using FluentValidation;

public sealed record GetOwnersQueryResult(PaginatedResult<OwnerListItemDto> Owners);

/// code | name | type (owner type name) | holdings (current holding count).
public enum OwnerSort { Code, Name, Type, Holdings }

/// What part a person or organization plays in the register (an owner record is also an allottee, a tenant, a party to a
/// case ...): holder = holds a current share; allottee / lessee / tenant / contractor = of an agreement in force;
/// bidder = bid in an auction; encroacher = named on an encroachment; litigant = a party to a case; appellant; violator.
public enum OwnerRole { Holder, Allottee, Lessee, Tenant, Contractor, Bidder, Encroacher, Litigant, Appellant, Violator }

/// Search matches the exact owner code, a CNIC (all 13 digits, or its first 5 or more), an NTN (either form) or part
/// of the name. Cnic / Ntn filter exactly, in any written form - the lookup to run before registering someone new
/// (send includeInactive=true so an inactive owner is found too). HasHoldings: true = owners holding at least one
/// current share (active or disputed, on an active property), false = none.
public sealed record GetOwnersQuery(
  PaginationRequest Pagination,
  string? Search = null,
  string? Cnic = null,
  string? Ntn = null,
  Guid? OwnerTypeId = null,
  bool IncludeInactive = false,
  bool? HasHoldings = null,
  OwnerSort SortBy = OwnerSort.Code,
  bool SortDescending = false,
  OwnerRole? Role = null) : IQuery<Result<GetOwnersQueryResult>>;

public class GetOwnersQueryValidator : AbstractValidator<GetOwnersQuery>
{
  public GetOwnersQueryValidator()
  {
    RuleFor(x => x.Pagination.Pageindex).GreaterThanOrEqualTo(0);
    RuleFor(x => x.Pagination.PageSize).InclusiveBetween(1, 200);
    RuleFor(x => x.SortBy).IsInEnum();
    RuleFor(x => x.Cnic).Must(c => global::Cnic.Normalize(c) is not null).When(x => !string.IsNullOrWhiteSpace(x.Cnic))
      .WithMessage("CNIC must have 13 digits (e.g. 12345-1234567-1).");
    RuleFor(x => x.Ntn).Must(n => global::Ntn.Normalize(n) is not null).When(x => !string.IsNullOrWhiteSpace(x.Ntn))
      .WithMessage("NTN must be 7 digits and a check digit (1234567-8) or a 13-digit CNIC-based number (12345-1234567-1).");
  }
}
