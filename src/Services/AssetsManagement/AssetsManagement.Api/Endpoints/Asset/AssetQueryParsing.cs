using System.Globalization;

/// Query-string parsing shared by the register (GET /assets) and the reports (GET /asset-reports/register), so the two
/// read the same filters the same way.
public static class AssetQueryParsing
{
  private const string MoneyRefused =
    "Filtering or sorting by purchase cost, purchase date, purchase record or depreciation needs the Asset finance right to view.";

  /// Enum query values are case-insensitive (sortBy=cost, disposed=only, ownership=leased).
  public static T? ParseEnum<T>(string? raw, string name) where T : struct, Enum
  {
    if (string.IsNullOrWhiteSpace(raw)) return null;
    if (Enum.TryParse<T>(raw.Trim(), ignoreCase: true, out var value) && Enum.IsDefined(value)) return value;
    throw new BadHttpRequestException($"'{raw}' is not a valid {name}. Use one of: {string.Join(", ", Enum.GetNames<T>().Select(n => n.ToLowerInvariant()))}.");
  }

  public static AttributeFilter ParseFilter(string? raw)
  {
    var parts = raw!.Split(':', 3);
    if (parts.Length != 3 || !Enum.TryParse<AttributeFilterOperator>(parts[1], ignoreCase: true, out var op))
      throw new BadHttpRequestException($"Invalid attribute filter '{raw}'. Expected code:op:value.");

    return new AttributeFilter(parts[0], op, parts[2]);
  }

  /// dynamic attribute filters: ?attr=ram_gb:gte:16&attr=operating_system:eq:WINDOWS_11
  public static List<AttributeFilter> AttributeFilters(HttpRequest http) =>
    http.Query["attr"].Where(v => !string.IsNullOrWhiteSpace(v)).Select(ParseFilter).ToList();

  /// The register's filters from the query string. Money columns come only with ASSET_FINANCE.VIEW, and a filter or
  /// sort that reads money is refused without it (a hidden filter would still reveal the figures by its results).
  public static GetAssetsQuery Read(HttpRequest http, ICurrentUser user, PaginationRequest pagination)
  {
    var q = http.Query;
    var query = new GetAssetsQuery(
      pagination,
      AssetClassId: Id(q, "assetClassId"),
      AssetTypeId: Id(q, "assetTypeId"),
      CategoryId: Id(q, "categoryId"),
      IncludeSubCategories: Flag(q, "includeSubCategories") ?? true,
      StatusId: Id(q, "statusId"),
      LocationId: Id(q, "locationId"),
      CustodianId: Id(q, "custodianId"),
      DepartmentId: Id(q, "departmentId"),
      Search: Text(q, "search"),
      IncludeInactive: Flag(q, "includeInactive") ?? false,
      AttributeFilters: AttributeFilters(http),
      IncludeSubLocations: Flag(q, "includeSubLocations") ?? true,
      Ownership: ParseEnum<OwnershipType>(Text(q, "ownership"), "ownership"),
      Disposed: ParseEnum<DisposalFilter>(Text(q, "disposed"), "disposed") ?? DisposalFilter.Include,
      RegisteredFrom: Instant(q, "registeredFrom"),
      RegisteredTo: Instant(q, "registeredTo"),
      AcquiredFrom: Day(q, "acquiredFrom"),
      AcquiredTo: Day(q, "acquiredTo"),
      CostMin: Amount(q, "costMin"),
      CostMax: Amount(q, "costMax"),
      SortBy: ParseEnum<AssetSort>(Text(q, "sortBy"), "sortBy") ?? AssetSort.Code,
      SortDescending: Flag(q, "sortDescending") ?? false,
      IncludeMoney: user.HasPermission(PermissionCatalog.AssetFinance.View),
      NoLocation: Flag(q, "noLocation") ?? false,
      Loan: ParseEnum<LoanFilter>(Text(q, "loan"), "loan"),
      WarrantyFrom: Day(q, "warrantyFrom"),
      WarrantyTo: Day(q, "warrantyTo"),
      DisposedFrom: Day(q, "disposedFrom"),
      DisposedTo: Day(q, "disposedTo"),
      HasPurchase: Flag(q, "hasPurchase"),
      DepreciationBehind: Flag(q, "depreciationBehind") ?? false,
      StatusSinceTo: Instant(q, "statusSinceTo"),
      IncludeSubUnits: Flag(q, "includeSubUnits") ?? true,
      MissingRequiredLocation: Flag(q, "missingRequiredLocation") ?? false,
      HeldOn: Day(q, "heldOn"),
      OutOfService: Flag(q, "outOfService") ?? false,
      InStatusOverDays: Number(q, "inStatusOverDays"),
      WarrantyWithinDays: Number(q, "warrantyWithinDays"),
      HasCustodian: Flag(q, "hasCustodian"));

    if (query.UsesMoney && !query.IncludeMoney)
      throw new AccessDeniedException(MoneyRefused);
    return query;
  }

  private static string? Text(IQueryCollection q, string name)
  {
    var raw = q[name].ToString();
    return string.IsNullOrWhiteSpace(raw) ? null : raw.Trim();
  }

  private static Guid? Id(IQueryCollection q, string name) =>
    Text(q, name) is { } raw ? Guid.TryParse(raw, out var v) ? v : throw Bad(name, raw) : null;

  private static bool? Flag(IQueryCollection q, string name) =>
    Text(q, name) is { } raw ? bool.TryParse(raw, out var v) ? v : throw Bad(name, raw) : null;

  private static DateOnly? Day(IQueryCollection q, string name) =>
    Text(q, name) is { } raw ? DateOnly.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var v) ? v : throw Bad(name, raw) : null;

  private static DateTime? Instant(IQueryCollection q, string name) =>
    Text(q, name) is { } raw
      ? DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var v) ? DateTime.SpecifyKind(v, DateTimeKind.Utc) : throw Bad(name, raw)
      : null;

  private static int? Number(IQueryCollection q, string name) =>
    Text(q, name) is { } raw ? int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : throw Bad(name, raw) : null;

  private static decimal? Amount(IQueryCollection q, string name) =>
    Text(q, name) is { } raw ? decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : throw Bad(name, raw) : null;

  private static BadHttpRequestException Bad(string name, string raw) => new($"'{raw}' is not a valid {name}.");
}
