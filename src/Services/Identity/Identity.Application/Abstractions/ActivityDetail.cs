/// How an activity line says WHAT changed, in two fixed shapes the Administration screens read back:
///
///  field changes   "Name: Clerk → Senior clerk; Active: Yes → No"   (only the fields that differ)
///  permissions     "+PROPERTY:VIEW,CREATE;ASSETS:* -HR:DELETE"      ("*" = every action of that module)
///
/// The detail column holds 500 characters; the recorder cuts anything longer.
public static class ActivityDetail
{
  public const string Arrow = " → ";

  /// The fields whose value differs, or null when nothing changed.
  public static string? Changes(params (string Label, string? Before, string? After)[] fields)
  {
    var changed = fields
      .Where(f => !string.Equals(f.Before ?? "", f.After ?? "", StringComparison.Ordinal))
      .Select(f => $"{f.Label}: {Show(f.Before)}{Arrow}{Show(f.After)}")
      .ToList();
    return changed.Count == 0 ? null : string.Join("; ", changed);
  }

  public static string YesNo(bool value) => value ? "Yes" : "No";

  /// Permission codes ("MODULE.ACTION") added and removed, grouped by module. `moduleSizes` is how
  /// many actions each module has, so a whole module reads "MODULE:*".
  public static string? Permissions(
      IEnumerable<string> added, IEnumerable<string> removed, IReadOnlyDictionary<string, int> moduleSizes)
  {
    var parts = new List<string>();
    var plus = Group(added, moduleSizes);
    var minus = Group(removed, moduleSizes);
    if (plus.Length > 0) parts.Add("+" + plus);
    if (minus.Length > 0) parts.Add("-" + minus);
    return parts.Count == 0 ? null : string.Join(" ", parts);
  }

  private static string Group(IEnumerable<string> codes, IReadOnlyDictionary<string, int> moduleSizes) =>
    string.Join(";", codes
      .Select(code => code.Split('.', 2))
      .Where(p => p.Length == 2)
      .GroupBy(p => p[0], p => p[1])
      .OrderBy(g => g.Key, StringComparer.Ordinal)
      .Select(g =>
      {
        var actions = g.Distinct().ToList();
        return moduleSizes.TryGetValue(g.Key, out var size) && size > 1 && actions.Count == size
          ? $"{g.Key}:*"
          : $"{g.Key}:{string.Join(",", actions)}";
      }));

  private static string Show(string? value) => string.IsNullOrWhiteSpace(value) ? "(none)" : value.Trim();
}
