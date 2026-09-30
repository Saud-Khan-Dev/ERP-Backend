using System.Text.RegularExpressions;

/// The stable code of a master (lookup) value: ACTIVE, SQFT, GIFT, CNIC_COPY ...
/// Code is what the application compares against — never the id (schema guide, rule 9).
public sealed record MasterCode
{
  public const int MaxLength = 30;

  public string Value { get; }

  private static readonly Regex Pattern = new(@"^[A-Z0-9][A-Z0-9_]*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

  private MasterCode(string value) => Value = value;

  public static MasterCode Of(string value)
  {
    if (string.IsNullOrWhiteSpace(value))
      throw new DomainException("Code is required.");

    value = value.Trim().ToUpperInvariant().Replace(' ', '_').Replace('-', '_');

    if (value.Length > MaxLength)
      throw new DomainException($"Code cannot exceed {MaxLength} characters.");

    if (!Pattern.IsMatch(value))
      throw new DomainException("Code may only contain letters, digits and underscores (e.g. OPEN_LAND).");

    return new MasterCode(value);
  }

  public override string ToString() => Value;
}
