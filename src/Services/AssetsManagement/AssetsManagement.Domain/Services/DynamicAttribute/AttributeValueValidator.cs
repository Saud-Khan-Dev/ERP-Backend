using System.Text.Json;

/// Validates asset.extra_attributes against the resolved schema (the ERD's BEFORE INSERT/UPDATE trigger, in C#):
/// rejects unknown keys, missing required keys, type mismatches, out-of-range values and read-only edits,
/// applies defaults and honours conditional visibility. Returns the canonical bag to persist.
public static class AttributeValueValidator
{
  /// input is a PATCH over existing: present keys are set (JSON null removes), absent keys are kept.
  public static Dictionary<string, JsonElement> Validate(
      ResolvedAttributeSchema schema,
      IReadOnlyDictionary<string, JsonElement> input,
      IReadOnlyDictionary<string, JsonElement>? existing,
      Func<OptionSetId, IReadOnlyCollection<string>?> optionCodes)
  {
    ArgumentNullException.ThrowIfNull(schema);
    ArgumentNullException.ThrowIfNull(input);
    ArgumentNullException.ThrowIfNull(optionCodes);

    var unknown = input.Keys.Where(k => !schema.Contains(k)).ToList();
    if (unknown.Count > 0)
      throw new DomainException($"Unknown attribute(s) for this asset: {string.Join(", ", unknown)}.");

    var merged = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
    if (existing is not null)
      foreach (var (code, value) in existing)
        if (schema.Contains(code) && !IsEmpty(value))
          merged[code] = value;

    foreach (var (code, value) in input)
    {
      if (IsEmpty(value)) merged.Remove(code);
      else merged[code] = value;
    }

    var result = new Dictionary<string, JsonElement>(StringComparer.Ordinal);

    foreach (var attribute in schema.Attributes)
    {
      var code = attribute.Code;
      var assignment = attribute.Assignment;
      var hasValue = merged.TryGetValue(code, out var raw);

      if (existing is not null && assignment.IsReadonly && input.TryGetValue(code, out var incoming))
      {
        var before = existing.TryGetValue(code, out var previous) ? previous.GetRawText() : null;
        var after = IsEmpty(incoming) ? null : incoming.GetRawText();
        if (!string.Equals(before, after, StringComparison.Ordinal))
          throw new DomainException($"'{attribute.Label}' is read-only.");
      }

      if (!hasValue && assignment.DefaultValue is { } fallback && !IsEmpty(fallback))
      {
        raw = fallback;
        hasValue = true;
      }

      var isVisible = IsVisible(schema, attribute, merged);

      if (!hasValue)
      {
        if (isVisible && assignment.IsRequired)
          throw new DomainException($"'{attribute.Label}' is required.");

        continue;
      }

      var options = attribute.Definition.OptionSetId is { } optionSetId ? optionCodes(optionSetId) : null;
      result[code] = attribute.Definition.NormalizeValue(raw, attribute.Label, options);
    }

    return result;
  }

  /// A dependent field only takes part in the form when its parent holds the configured value(s).
  private static bool IsVisible(ResolvedAttributeSchema schema, ResolvedAttribute attribute, IReadOnlyDictionary<string, JsonElement> values)
  {
    var assignment = attribute.Assignment;
    if (assignment.DependsOnAssignmentId is null)
      return true;

    var parent = schema.FindByAssignmentId(assignment.DependsOnAssignmentId);
    if (parent is null)
      return true;

    if (!values.TryGetValue(parent.Code, out var parentValue) || IsEmpty(parentValue))
      return false;

    if (assignment.DependsOnValue is not { } expected || IsEmpty(expected))
      return true;

    var actual = Flatten(parentValue);
    var wanted = Flatten(expected);

    return actual.Any(a => wanted.Contains(a, StringComparer.OrdinalIgnoreCase));
  }

  private static IReadOnlyList<string> Flatten(JsonElement element) =>
      element.ValueKind == JsonValueKind.Array
          ? element.EnumerateArray().Select(AsComparable).ToList()
          : new[] { AsComparable(element) };

  private static string AsComparable(JsonElement element) =>
      element.ValueKind == JsonValueKind.String ? element.GetString()! : element.GetRawText();

  public static bool IsEmpty(JsonElement element) =>
      element.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null
      || (element.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(element.GetString()))
      || (element.ValueKind == JsonValueKind.Array && element.GetArrayLength() == 0);
}
