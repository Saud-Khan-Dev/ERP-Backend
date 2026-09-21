public sealed record GetLifecycleEventTypesQueryResult(IReadOnlyList<LifecycleEventTypeDto> EventTypes);

public sealed record GetLifecycleEventTypesQuery(string? Stage, bool IncludeInactive) : IQuery<Result<GetLifecycleEventTypesQueryResult>>;
