using System.Text.RegularExpressions;

public sealed record Code
{
  private const int DefaultLength = 50;
  public string Value { get; }

  private readonly static Regex Pattern = new Regex(@"^[A-Z]{3}-\d{3}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
  private Code(string value) => Value = value;
  public static Code Of(string value)
  {
    if (string.IsNullOrWhiteSpace(value))
      throw new DomainException("Category code is required.");

    if (value.Length > DefaultLength)
      throw new DomainException("Category code is too long.");

    value = value.Trim().ToUpperInvariant();

    if (!Pattern.IsMatch(value))
      throw new DomainException(
          "Category code must be in the format CAT-001.");
          
    return new Code(value);
  }
}