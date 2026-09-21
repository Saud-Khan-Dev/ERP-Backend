public sealed record GetDepreciationSchedulesQueryResult(IReadOnlyList<AssetDepreciationScheduleDto> Schedules, decimal? NetBookValue);

public sealed record GetDepreciationSchedulesQuery(Guid AssetId, bool IncludeInactive) : IQuery<Result<GetDepreciationSchedulesQueryResult>>;
