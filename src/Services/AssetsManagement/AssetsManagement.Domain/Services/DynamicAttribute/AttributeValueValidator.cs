using System.Text.Json;

/// Validates asset.extra_attributes against the resolved schema (the ERD's BEFORE INSERT/UPDATE trigger, in C#):
/// rejects values for fields that are not on the form, missing required values, type mismatches, out-of-range values
/// and read-only edits, applies defaults and honours conditional visibility. Returns the canonical bag to persist.
///
/// A save never erases what is stored:
/// - a value of a field that is no longer on the asset's form (removed from its node, hidden, switched off, or the
///   asset moved to another category) is kept as it is - shown read-only on the record;
/// - only values that CHANGED are checked again, so an asset that holds a choice that has since been switched off
///   (or a value an older rule allowed) can still be saved with it.
public static class AttributeValueValidator
{
  /// input is a PATCH over existing: present keys are set (JSON null removes), absent keys are kept.
  /// optionSet: the option set of a SELECT / MULTISELECT field.
  public static Dictionary<string, JsonElement> Validate(
      ResolvedAttributeSchema schema,
      IReadOnlyDictionary<string, JsonElement> input,
      IReadOnlyDictionary<string, JsonElement>? existing,
      Func<OptionSetId, OptionSet?> optionSet)
  {
    ArgumentNullException.ThrowIfNull(schema);
    ArgumentNullException.ThrowIfNull(input);
    ArgumentNullException.ThrowIfNull(optionSet);

    var unknown = input.Where(kv => !schema.Contains(kv.Key) && !IsEmpty(kv.Value)).Select(kv => kv.Key).ToList();
    if (unknown.Count > 0)
      throw new DomainException(unknown.Count == 1
        ? $"The field '{unknown[0]}' is not on this asset's form."
        : $"These fields are not on this asset's form: {string.Join(", ", unknown)}.");

    // every stored value, then the patch - an empty value removes a field's value only when the field is on the form
    var merged = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
    if (existing is not null)
      foreach (var (code, value) in existing)
        if (!IsEmpty(value))
          merged[code] = value;

    foreach (var (code, value) in input)
    {
      if (!schema.Contains(code)) continue;
      if (IsEmpty(value)) merged.Remove(code);
      else merged[code] = value;
    }

    var result = new Dictionary<string, JsonElement>(StringComparer.Ordinal);

    // values of fields that are not on the form any more stay exactly as they were
    if (existing is not null)
      foreach (var (code, value) in existing)
        if (!schema.Contains(code) && !IsEmpty(value))
          result[code] = value;

    foreach (var attribute in schema.Attributes)
    {
      var code = attribute.Code;
      var assignment = attribute.Assignment;
      var hasValue = merged.TryGetValue(code, out var raw);
      JsonElement previous = default;
      var hadValue = existing is not null && existing.TryGetValue(code, out previous) && !IsEmpty(previous);

      if (existing is not null && assignment.IsReadonly && input.TryGetValue(code, out var incoming))
      {
        var before = hadValue ? previous.GetRawText() : null;
        var after = IsEmpty(incoming) ? null : incoming.GetRawText();
        if (!string.Equals(before, after, StringComparison.Ordinal))
          throw new DomainException($"'{attribute.Label}' cannot be changed here.");
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

      // unchanged since the last save: kept as stored, not checked against today's rules again
      if (hadValue && string.Equals(previous.GetRawText(), raw.GetRawText(), StringComparison.Ordinal))
      {
        result[code] = previous;
        continue;
      }

      var choices = attribute.Definition.OptionSetId is { } optionSetId && optionSet(optionSetId) is { } set
        ? set.Choices(hadValue ? Flatten(previous) : [])
        : null;
      result[code] = attribute.Definition.NormalizeValue(raw, attribute.Label, choices);
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
