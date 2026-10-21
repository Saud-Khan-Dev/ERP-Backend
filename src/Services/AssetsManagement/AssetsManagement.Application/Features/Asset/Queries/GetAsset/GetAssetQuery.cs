public sealed record GetAssetQueryResult(AssetDto Asset, IReadOnlyList<ResolvedAttributeDto> AttributeSchema, IReadOnlyList<RetiredAttributeDto> RetiredAttributes);

/// The asset plus its resolved form (so a UI can render the dynamic fields with a single call), and the fields it holds
/// values for that are no longer on its form (kept, shown read-only).
public sealed record GetAssetQuery(Guid Id) : IQuery<Result<GetAssetQueryResult>>;
