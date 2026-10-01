/// Query-string parsing shared by the register (GET /assets) and the reports (GET /asset-reports/register).
public static class AssetQueryParsing
{
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
}
