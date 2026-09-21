using System.Text.RegularExpressions;

/// Admin-defined code for taxonomy / lookup rows: PHYSICAL, IT_EQUIPMENT, STRAIGHT_LINE, OPERATING_SYSTEM ...
/// Normalized to upper snake case so it can double as a stable ltree label (lower-cased) and a JSON key.
public sealed record LookupCode
{
  public const int MaxLength = 100;
  public string Value { get; }

  private static readonly Regex Pattern = new(@"^[A-Z0-9][A-Z0-9_]*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

  private LookupCode(string value) => Value = value;

  public static LookupCode Of(string value)
  {
    if (string.IsNullOrWhiteSpace(value))
      throw new DomainException("Code is required.");

    value = value.Trim().ToUpperInvariant().Replace(' ', '_').Replace('-', '_');

    if (value.Length > MaxLength)
      throw new DomainException($"Code cannot exceed {MaxLength} characters.");

    if (!Pattern.IsMatch(value))
      throw new DomainException("Code may only contain letters, digits and underscores (e.g. IT_EQUIPMENT).");

    return new LookupCode(value);
  }

  /// ltree-safe label: lower case, same characters.
  public string ToPathLabel() => Value.ToLowerInvariant();
}
