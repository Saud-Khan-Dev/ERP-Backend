using System.Text.RegularExpressions;

/// Human readable asset tag, e.g. AST-000123.
public sealed record AssetCode
{
  public const int MaxLength = 50;
  public string Value { get; }

  private static readonly Regex Pattern = new(@"^[A-Z0-9][A-Z0-9\-_/]*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

  private AssetCode(string value) => Value = value;

  public static AssetCode Of(string value)
  {
    if (string.IsNullOrWhiteSpace(value))
      throw new DomainException("Asset code is required.");

    value = value.Trim().ToUpperInvariant();

    if (value.Length > MaxLength)
      throw new DomainException($"Asset code cannot exceed {MaxLength} characters.");

    if (!Pattern.IsMatch(value))
      throw new DomainException("Asset code may only contain letters, digits, '-', '_' and '/' (e.g. AST-000123).");

    return new AssetCode(value);
  }
}
