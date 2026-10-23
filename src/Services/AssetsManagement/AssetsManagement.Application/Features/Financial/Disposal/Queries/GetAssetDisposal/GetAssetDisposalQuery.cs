public sealed record GetAssetDisposalQueryResult(AssetDisposalDto Disposal);

/// The asset's live disposal: the proposal waiting for approval, or the approved one. 404 when there is neither.
public sealed record GetAssetDisposalQuery(Guid AssetId) : IQuery<Result<GetAssetDisposalQueryResult>>;

public sealed record GetAssetDisposalsQueryResult(IReadOnlyList<AssetDisposalDto> Disposals);

/// Every disposal of the asset, newest first - the live one and those rejected, withdrawn or cancelled.
public sealed record GetAssetDisposalsQuery(Guid AssetId) : IQuery<Result<GetAssetDisposalsQueryResult>>;
