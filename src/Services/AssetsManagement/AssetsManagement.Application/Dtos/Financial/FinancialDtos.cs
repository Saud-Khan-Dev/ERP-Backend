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
  string? ReversedByName);

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

public sealed record AssetDisposalDto(
  Guid Id,
  Guid AssetId,
  DateOnly DisposalDate,
  Guid DisposalMethodId,
  decimal? DisposalValue,
  string? CurrencyCode,
  decimal? NetBookValueAtDisposal,
  decimal? GainLoss,
  string? BuyerInfo,
  string? Reason,
  Guid? ApprovedBy,
  string? ApprovedByName,
  DateTime? ApprovedAt);

public static class FinancialMappings
{
  public static AssetDepreciationEntryDto ToDto(this AssetDepreciationEntry x) => new(
    x.Id.Value, x.ScheduleId.Value, x.PeriodStart, x.PeriodEnd, x.LastDay, x.OpeningBookValue, x.DepreciationAmount,
    x.AccumulatedDepreciation, x.BookValueAfter, x.Posted, x.PostedAt, x.PostedBy, x.PostedByName, x.ReversedAt, x.ReversedBy, x.ReversedByName);

  public static AssetDepreciationScheduleDto ToDto(this AssetDepreciationSchedule x) => new(
    x.Id.Value, x.AssetId.Value, x.MethodId.Value, x.UsefulLifeMonths, x.SalvageValue, x.OpeningBookValue, x.DepreciableBase,
    x.DecliningRate, x.StartDate, x.EndDate, x.IsActive,
    x.Entries.OrderBy(e => e.PeriodStart).Select(e => e.ToDto()).ToList());

  public static AssetValuationDto ToDto(this AssetValuation x) => new(
    x.Id.Value, x.AssetId.Value, x.ValuationDate, x.Value, x.CurrencyCode.Value, x.ValuationMethod, x.ValuedByName, x.Notes,
    x.RecordedBy, x.RecordedByName);

  public static AssetDisposalDto ToDto(this AssetDisposal x) => new(
    x.Id.Value, x.AssetId.Value, x.DisposalDate, x.DisposalMethodId.Value, x.DisposalValue, x.CurrencyCode?.Value,
    x.NetBookValueAtDisposal, x.GainLoss, x.BuyerInfo, x.Reason, x.ApprovedBy, x.ApprovedByName, x.ApprovedAt);
}
