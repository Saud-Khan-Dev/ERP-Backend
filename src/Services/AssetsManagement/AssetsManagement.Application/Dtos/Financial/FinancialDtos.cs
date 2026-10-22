/// PeriodEnd is the first day after the period; LastDay is its own last day (show "PeriodStart - LastDay").
public sealed record AssetDepreciationEntryDto(
  Guid Id,
  Guid ScheduleId,
  DateOnly PeriodStart,
  DateOnly PeriodEnd,
  DateOnly LastDay,
  decimal OpeningBookValue,
  decimal DepreciationAmount,
  decimal AccumulatedDepreciation,
  decimal BookValueAfter,
  bool Posted,
  DateTime? PostedAt,
  Guid? PostedBy,
  string? PostedByName,
  DateTime? ReversedAt,
  Guid? ReversedBy,
  string? ReversedByName,
  string? ReversalReason);

public sealed record AssetDepreciationScheduleDto(
  Guid Id,
  Guid AssetId,
  Guid MethodId,
  int UsefulLifeMonths,
  decimal SalvageValue,
  decimal OpeningBookValue,
  decimal DepreciableBase,
  decimal? DecliningRate,
  DateOnly StartDate,
  DateOnly? EndDate,
  bool IsActive,
  IReadOnlyList<AssetDepreciationEntryDto> Entries);

public sealed record AssetValuationDto(
  Guid Id,
  Guid AssetId,
  DateOnly ValuationDate,
  decimal Value,
  string CurrencyCode,
  string? ValuationMethod,
  string? ValuedByName,
  string? Notes,
  Guid? RecordedBy,
  string? RecordedByName);

/// A disposal: State Proposed (waiting for approval; no book value or gain / loss yet), Approved (the asset left),
/// Rejected or Withdrawn (the proposal was turned down or taken back - DecidedBy, DecidedAt, DecisionNote), Cancelled
/// (an approved disposal undone as a mistake - CancelledBy, CancelledAt, CancellationReason).
public sealed record AssetDisposalDto(
  Guid Id,
  Guid AssetId,
  DisposalState State,
  DateOnly DisposalDate,
  Guid DisposalMethodId,
  Guid? ToStatusId,
  decimal? DisposalValue,
  string? CurrencyCode,
  decimal? NetBookValueAtDisposal,
  decimal? GainLoss,
  string? BuyerInfo,
  string? Reason,
  Guid? ProposedBy,
  string? ProposedByName,
  DateTime? ProposedAt,
  Guid? ApprovedBy,
  string? ApprovedByName,
  DateTime? ApprovedAt,
  Guid? DecidedBy,
  string? DecidedByName,
  DateTime? DecidedAt,
  string? DecisionNote,
  Guid? CancelledBy,
  string? CancelledByName,
  DateTime? CancelledAt,
  string? CancellationReason);

public static class FinancialMappings
{
  public static AssetDepreciationEntryDto ToDto(this AssetDepreciationEntry x) => new(
    x.Id.Value, x.ScheduleId.Value, x.PeriodStart, x.PeriodEnd, x.LastDay, x.OpeningBookValue, x.DepreciationAmount,
    x.AccumulatedDepreciation, x.BookValueAfter, x.Posted, x.PostedAt, x.PostedBy, x.PostedByName, x.ReversedAt, x.ReversedBy, x.ReversedByName,
    x.ReversalReason);

  public static AssetDepreciationScheduleDto ToDto(this AssetDepreciationSchedule x) => new(
    x.Id.Value, x.AssetId.Value, x.MethodId.Value, x.UsefulLifeMonths, x.SalvageValue, x.OpeningBookValue, x.DepreciableBase,
    x.DecliningRate, x.StartDate, x.EndDate, x.IsActive,
    x.Entries.OrderBy(e => e.PeriodStart).Select(e => e.ToDto()).ToList());

  public static AssetValuationDto ToDto(this AssetValuation x) => new(
    x.Id.Value, x.AssetId.Value, x.ValuationDate, x.Value, x.CurrencyCode.Value, x.ValuationMethod, x.ValuedByName, x.Notes,
    x.RecordedBy, x.RecordedByName);

  public static AssetDisposalDto ToDto(this AssetDisposal x) => new(
    x.Id.Value, x.AssetId.Value, x.State, x.DisposalDate, x.DisposalMethodId.Value, x.ToStatusId?.Value, x.DisposalValue, x.CurrencyCode?.Value,
    x.NetBookValueAtDisposal, x.GainLoss, x.BuyerInfo, x.Reason, x.ProposedBy, x.ProposedByName, x.ProposedAt,
    x.ApprovedBy, x.ApprovedByName, x.ApprovedAt, x.DecidedBy, x.DecidedByName, x.DecidedAt, x.DecisionNote,
    x.CancelledBy, x.CancelledByName, x.CancelledAt, x.CancellationReason);
}
