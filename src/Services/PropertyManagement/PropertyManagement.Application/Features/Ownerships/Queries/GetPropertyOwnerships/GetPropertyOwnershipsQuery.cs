/// AllocatedSharePct: total of the property's current shares (at most 100). Null for an owner's list,
/// where shares belong to different properties and cannot be added up.
public sealed record GetOwnershipsQueryResult(decimal? AllocatedSharePct, IReadOnlyList<OwnershipDto> Ownerships);

/// Current owners by default; includeHistory adds ended and disputed rows (the ownership history).
public sealed record GetPropertyOwnershipsQuery(Guid PropertyId, bool IncludeHistory = false) : IQuery<Result<GetOwnershipsQueryResult>>;

/// Everything one owner holds or held, across properties.
public sealed record GetOwnerOwnershipsQuery(Guid OwnerId, bool IncludeHistory = false) : IQuery<Result<GetOwnershipsQueryResult>>;
