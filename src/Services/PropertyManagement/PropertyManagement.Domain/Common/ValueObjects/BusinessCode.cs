using System.Text.RegularExpressions;

/// A generated business number: PROP-00001, OWN-00001, TRF-00001 ...
/// Issued from a CodeSequence and never typed by users (schema guide, rule 8).
public sealed record BusinessCode
{
  public const int MaxLength = 50;

  public string Value { get; }

  private static readonly Regex Pattern = new(@"^[A-Z0-9][A-Z0-9\-_/]*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

  private BusinessCode(string value) => Value = value;

  public static BusinessCode Of(string value)
  {
    if (string.IsNullOrWhiteSpace(value))
      throw new DomainException("Code is required.");

    value = value.Trim().ToUpperInvariant();

    if (value.Length > MaxLength)
      throw new DomainException($"Code cannot exceed {MaxLength} characters.");

    if (!Pattern.IsMatch(value))
      throw new DomainException("Code may only contain letters, digits, '-', '_' and '/' (e.g. PROP-00001).");

    return new BusinessCode(value);
  }

  public override string ToString() => Value;
}
