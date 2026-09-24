using System.Text.RegularExpressions;

public sealed record EmailAddress
{
  public const int MaxLength = 255;

  public string Value { get; }

  private static readonly Regex Pattern = new(@"^[^@\s]+@[^@\s.]+(\.[^@\s.]+)+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

  private EmailAddress(string value) => Value = value;

  public static EmailAddress Of(string value)
  {
    if (string.IsNullOrWhiteSpace(value))
      throw new DomainException("Email is required.");

    value = value.Trim().ToLowerInvariant();

    if (value.Length > MaxLength)
      throw new DomainException($"Email cannot exceed {MaxLength} characters.");

    if (!Pattern.IsMatch(value))
      throw new DomainException("Email is not a valid address.");

    return new EmailAddress(value);
  }

  public override string ToString() => Value;
}
