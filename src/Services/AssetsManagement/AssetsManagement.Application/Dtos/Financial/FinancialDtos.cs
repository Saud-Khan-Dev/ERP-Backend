public sealed record AssetDepreciationEntryDto(
  Guid Id,
  Guid ScheduleId,
  DateOnly PeriodStart,
  DateOnly PeriodEnd,
  decimal OpeningBookValue,
  decimal DepreciationAmount,
  decimal AccumulatedDepreciation,
  decimal BookValueAfter,
  bool Posted,
  DateTime? PostedAt,
  Guid? PostedBy,
  DateTime? ReversedAt,
  Guid? ReversedBy);

public sealed record AssetDepreciationScheduleDto(
  Guid Id,
  Guid AssetId,
  Guid MethodId,
  int UsefulLifeMonths,
  decimal SalvageValue,
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
  Guid? ValuedBy,
  string? Notes);

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
  DateTime? ApprovedAt);

public static class FinancialMappings
{
  public static AssetDepreciationEntryDto ToDto(this AssetDepreciationEntry x) => new(
    x.Id.Value, x.ScheduleId.Value, x.PeriodStart, x.PeriodEnd, x.OpeningBookValue, x.DepreciationAmount,
    x.AccumulatedDepreciation, x.BookValueAfter, x.Posted, x.PostedAt, x.PostedBy, x.ReversedAt, x.ReversedBy);

  public static AssetDepreciationScheduleDto ToDto(this AssetDepreciationSchedule x) => new(
    x.Id.Value, x.AssetId.Value, x.MethodId.Value, x.UsefulLifeMonths, x.SalvageValue, x.DepreciableBase,
    x.DecliningRate, x.StartDate, x.EndDate, x.IsActive,
    x.Entries.OrderBy(e => e.PeriodStart).Select(e => e.ToDto()).ToList());

  public static AssetValuationDto ToDto(this AssetValuation x) => new(
    x.Id.Value, x.AssetId.Value, x.ValuationDate, x.Value, x.CurrencyCode.Value, x.ValuationMethod, x.ValuedBy, x.Notes);

  public static AssetDisposalDto ToDto(this AssetDisposal x) => new(
    x.Id.Value, x.AssetId.Value, x.DisposalDate, x.DisposalMethodId.Value, x.DisposalValue, x.CurrencyCode?.Value,
    x.NetBookValueAtDisposal, x.GainLoss, x.BuyerInfo, x.Reason, x.ApprovedBy, x.ApprovedAt);
}
