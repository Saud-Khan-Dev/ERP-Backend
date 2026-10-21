/// The asset's schedules with their months, and its ONE book value now: cost less every posted, not reversed month
/// of every schedule. PostedThrough = the last day of the last posted month; DraftMonths = months written, not posted.
public sealed record GetDepreciationSchedulesQueryResult(
  IReadOnlyList<AssetDepreciationScheduleDto> Schedules,
  decimal? NetBookValue,
  decimal WrittenOff,
  DateOnly? PostedThrough,
  int DraftMonths);

public sealed record GetDepreciationSchedulesQuery(Guid AssetId, bool IncludeInactive) : IQuery<Result<GetDepreciationSchedulesQueryResult>>;
