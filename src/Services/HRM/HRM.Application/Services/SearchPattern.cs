/// LIKE patterns from what a person typed, for case-insensitive search: compare against the column's ToLower() with
/// EF.Functions.Like(column.ToLower(), pattern, SearchPattern.Escape). % and _ typed by a person match themselves.
public static class SearchPattern
{
  public const string Escape = "\\";

  public static string Contains(string term) => $"%{Quote(term.Trim().ToLowerInvariant())}%";

  public static string StartsWith(string term) => $"{Quote(term.Trim().ToLowerInvariant())}%";

  private static string Quote(string term) => term.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
