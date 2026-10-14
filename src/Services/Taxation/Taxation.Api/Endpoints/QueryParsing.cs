using System.Text.RegularExpressions;

/// Query-string parsing shared by the lists and reports.
public static class QueryParsing
{
  /// Enum query values are case-insensitive (status=pending, employmentType=regular).
  public static T? ParseEnum<T>(string? raw, string name) where T : struct, Enum
  {
    if (string.IsNullOrWhiteSpace(raw)) return null;
    var text = raw.Trim().Replace("_", string.Empty);
    if (Enum.TryParse<T>(text, ignoreCase: true, out var value) && Enum.IsDefined(value) && !int.TryParse(raw, out _)) return value;
    throw new BadHttpRequestException($"'{raw}' is not a valid {name}. Use one of: {string.Join(", ", Enum.GetNames<T>().Select(SnakeCase))}.");
  }

  private static string SnakeCase(string name) => Regex.Replace(name, "(?<=[a-z0-9])([A-Z])", "_$1").ToLowerInvariant();

  /// sortDir=asc|desc; null when not given.
  public static bool? ParseDescending(string? sortDir) => sortDir?.Trim().ToLowerInvariant() switch
  {
    null or "" => null,
    "asc" => false,
    "desc" => true,
    _ => throw new BadHttpRequestException($"'{sortDir}' is not a valid sortDir. Use asc or desc.")
  };

  public static PaginationRequest Page(int? pageIndex, int? pageSize, int defaultSize = 20) => new(pageIndex ?? 0, pageSize ?? defaultSize);
}
