public sealed record GetAssetQueryResult(AssetDto Asset, IReadOnlyList<ResolvedAttributeDto> AttributeSchema);

/// The asset plus its resolved form (so a UI can render the dynamic fields with a single call).
public sealed record GetAssetQuery(Guid Id) : IQuery<Result<GetAssetQueryResult>>;
