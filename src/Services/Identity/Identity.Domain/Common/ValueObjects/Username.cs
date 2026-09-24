using System.Text.RegularExpressions;

/// Login name. Normalized to lower case so "Ahmed" and "ahmed" can never become two accounts.
public sealed record Username
{
  public const int MaxLength = 100;
  private const int MinLength = 3;

  public string Value { get; }

  private static readonly Regex Pattern = new(@"^[a-z0-9][a-z0-9._-]*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

  private Username(string value) => Value = value;

  public static Username Of(string value)
  {
    if (string.IsNullOrWhiteSpace(value))
      throw new DomainException("Username is required.");

    value = value.Trim().ToLowerInvariant();

    if (value.Length is < MinLength or > MaxLength)
      throw new DomainException($"Username must be between {MinLength} and {MaxLength} characters.");

    if (!Pattern.IsMatch(value))
      throw new DomainException("Username may only contain lower-case letters, digits, '.', '_' and '-', and must start with a letter or digit.");

    return new Username(value);
  }

  public override string ToString() => Value;
}
