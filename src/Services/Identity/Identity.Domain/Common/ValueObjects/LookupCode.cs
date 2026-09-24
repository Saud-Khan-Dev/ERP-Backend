using System.Text.RegularExpressions;

/// Upper snake-case code for roles and permission modules: SUPER_ADMIN, ASSET_MANAGER, ASSETS ...
public sealed record LookupCode
{
  public const int MaxLength = 50;

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
      throw new DomainException("Code may only contain upper-case letters, digits and underscores (e.g. ASSET_MANAGER).");

    return new LookupCode(value);
  }

  public override string ToString() => Value;
}
