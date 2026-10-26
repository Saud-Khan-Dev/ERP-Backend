using System.Globalization;

/// The Assets audit trail's words: the kinds of record its lines are about, and the lines the acts write themselves
/// (verbs, runs, field values) next to the ones the interceptor writes for every saved change.
public static class AssetAuditKinds
{
  public const string Module = "Assets";

  public const string Asset = "Asset";
  public const string Move = "Move";
  public const string Purchase = "Purchase";
  public const string Document = "Document";
  public const string Event = "Event";
  public const string DepreciationSchedule = "Depreciation schedule";
  public const string DepreciationMonth = "Depreciation month";
  public const string DepreciationRun = "Depreciation run";
  public const string Valuation = "Valuation";
  public const string Disposal = "Disposal";

  /// Lines about money: an asset's History shows them to a person without ASSET_FINANCE.VIEW as "Restricted" (the act,
  /// who and when, but no figures).
  public static readonly IReadOnlySet<string> Money = new HashSet<string>(StringComparer.Ordinal)
  {
    Purchase, DepreciationSchedule, DepreciationMonth, DepreciationRun, Valuation, Disposal
  };
}

/// The verbs the Assets acts write in addition to the field changes.
public static class AssetAuditActions
{
  public const string Recorded = "Recorded";
  public const string Posted = "Posted";
  public const string Reversed = "Reversed";
  public const string Approved = "Approved";
  public const string Rejected = "Rejected";
  public const string Withdrawn = "Withdrawn";
  public const string Cancelled = "Cancelled";
}

public static class AssetAudit
{
  /// "AST-000041 · Dell Latitude 5440" - the same words the interceptor writes for an asset as a parent.
  public static string Name(Asset asset) => $"{asset.AssetCode.Value} · {asset.Name.Value}";

  /// A line about something under the asset (a month, a run for this asset, a field value): its History lists it.
  public static void Line(this IAuditTrail audit, string action, string kind, Asset asset, Guid? recordId = null, string? recordCode = null,
      string? recordLabel = null, string? detail = null, string? field = null, string? oldValue = null, string? newValue = null) =>
    audit.Record(action, kind, recordId ?? asset.Id.Value, recordCode ?? (kind == AssetAuditKinds.Asset ? asset.AssetCode.Value : null),
      recordLabel ?? (kind == AssetAuditKinds.Asset ? asset.Name.Value : null), asset.Id.Value, Name(asset), detail, field, oldValue, newValue);

  /// "1 Oct 2026 - 31 Oct 2026"
  public static string Period(AssetDepreciationEntry entry) =>
    $"{entry.PeriodStart.ToString("dd'/'MM'/'yyyy", CultureInfo.InvariantCulture)} - {entry.LastDay.ToString("dd'/'MM'/'yyyy", CultureInfo.InvariantCulture)}";

  /// "Oct 2026" - a month as its code in the trail.
  public static string Month(AssetDepreciationEntry entry) => entry.PeriodStart.ToString("MMM yyyy", CultureInfo.InvariantCulture);

  public static string Amount(decimal value) => value.ToString("N2", CultureInfo.InvariantCulture);

  public static string Day(DateOnly day) => day.ToString("dd'/'MM'/'yyyy", CultureInfo.InvariantCulture);

  /// Parts joined the trail's way, empty ones left out.
  public static string? Join(params string?[] parts)
  {
    var kept = parts.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
    return kept.Count == 0 ? null : string.Join(" · ", kept);
  }
}
