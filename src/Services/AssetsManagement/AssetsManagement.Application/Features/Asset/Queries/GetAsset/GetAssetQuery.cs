public sealed record GetAssetQueryResult(AssetDto Asset, IReadOnlyList<ResolvedAttributeDto> AttributeSchema, IReadOnlyList<RetiredAttributeDto> RetiredAttributes,
  PendingDisposalDto? PendingDisposal);

/// A disposal proposed and waiting for approval: who proposed it, when, and for which day. No money - the record shows
/// it to everyone who can open the asset (the figures are on the Finance tab, under ASSET_FINANCE.VIEW).
public sealed record PendingDisposalDto(Guid Id, DateOnly DisposalDate, Guid? ProposedBy, string? ProposedByName, DateTime? ProposedAt);

/// The asset plus its resolved form (so a UI can render the dynamic fields with a single call), the fields it holds
/// values for that are no longer on its form (kept, shown read-only), and a disposal waiting for approval.
public sealed record GetAssetQuery(Guid Id) : IQuery<Result<GetAssetQueryResult>>;
