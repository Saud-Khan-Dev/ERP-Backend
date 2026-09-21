public sealed record GetAssetLifecycleEventsQueryResult(IReadOnlyList<AssetLifecycleEventDto> Events);

public sealed record GetAssetLifecycleEventsQuery(Guid AssetId, Guid? EventTypeId) : IQuery<Result<GetAssetLifecycleEventsQueryResult>>;
