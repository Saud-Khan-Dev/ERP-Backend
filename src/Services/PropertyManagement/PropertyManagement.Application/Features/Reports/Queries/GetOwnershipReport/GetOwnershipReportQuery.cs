public sealed record GetOwnershipReportQueryResult(DateOnly AsOf, int Count, IReadOnlyList<OwnershipReportRowDto> Rows);

/// Who held what share of which property on AsOf (default today). Search matches part of the property's code or
/// name, the owner's code, name or CNIC, or the ownership's reference no. IncludeDisputed adds shares marked
/// disputed; IncludeInactive adds properties taken off the register.
public sealed record GetOwnershipReportQuery(
  DateOnly? AsOf = null,
  Guid? TownId = null,
  Guid? OwnerTypeId = null,
  Guid? PropertyId = null,
  Guid? OwnerId = null,
  string? Search = null,
  bool IncludeDisputed = false,
  bool IncludeInactive = false) : IQuery<Result<GetOwnershipReportQueryResult>>;
