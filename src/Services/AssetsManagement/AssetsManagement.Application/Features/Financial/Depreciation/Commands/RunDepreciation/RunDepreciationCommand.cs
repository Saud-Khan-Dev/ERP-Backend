public sealed record DepreciationRunSkip(Guid AssetId, string AssetCode, string Reason);

public sealed record RunDepreciationCommandResult(int Schedules, int EntriesGenerated, int EntriesPosted, decimal AmountGenerated, IReadOnlyList<DepreciationRunSkip> Skipped);

/// Period-end depreciation for EVERY active schedule: the missing monthly entries up to `Until` are generated and,
/// when Post is set, every unposted entry ending on or before `Until` is posted. A schedule that cannot run
/// (units-of-production, no acquisition) is skipped and reported; it never stops the others.
/// AssetId limits the run to one asset's schedule.
public sealed record RunDepreciationCommand(DateOnly? Until, bool Post, Guid? PostedBy = null, Guid? AssetId = null) : ICommand<Result<RunDepreciationCommandResult>>;
