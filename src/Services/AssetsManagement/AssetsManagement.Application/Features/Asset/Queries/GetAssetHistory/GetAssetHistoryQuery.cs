/// One line of an asset's History - the audit trail's line as the record shows it. Restricted: a line about money
/// (purchase, depreciation, valuation, disposal) shown to a person without ASSET_FINANCE.VIEW - the act, who and when
/// stay; the figures (old / new value, detail) are left out and the screen says "Restricted".
public sealed record AssetHistoryLineDto(
  Guid Id,
  DateTimeOffset At,
  Guid? ActorId,
  string ActorName,
  string Action,
  string RecordKind,
  Guid? RecordId,
  string? RecordCode,
  string? RecordLabel,
  string? Field,
  string? OldValue,
  string? NewValue,
  string? Detail,
  string? RequestId,
  bool Restricted);

public sealed record AssetHistoryPageDto(IReadOnlyList<AssetHistoryLineDto> Data, long Count);

public sealed record GetAssetHistoryQueryResult(AssetHistoryPageDto History);

/// Everything the audit trail holds about one asset and what hangs off it (moves, purchase, documents, depreciation,
/// valuations, disposals, field values), newest first. Filter: before (continue a listing), from / to, action, kind,
/// search, pageSize (1-200, default 50).
public sealed record GetAssetHistoryQuery(Guid AssetId, AuditFilter Filter) : IQuery<Result<GetAssetHistoryQueryResult>>;
