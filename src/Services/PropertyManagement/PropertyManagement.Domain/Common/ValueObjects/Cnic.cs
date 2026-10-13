/// Pakistani CNIC. Accepts 13 digits with or without dashes or spaces; stored as 12345-1234567-1.
/// A NADRA number never starts with 0 (the first digit is the province), so a leading 0 or all zeros is refused.
public sealed record Cnic
{
  public const int MaxLength = 15;
  public const int DigitCount = 13;

  public string Value { get; }

  private Cnic(string value) => Value = value;

  public static Cnic Of(string value)
  {
    if (string.IsNullOrWhiteSpace(value))
      throw new DomainException("CNIC is required.");

    var digits = DigitsOf(value);

    if (digits.Length != DigitCount || !digits.All(char.IsAsciiDigit))
      throw new DomainException("CNIC must have 13 digits (e.g. 12345-1234567-1).");

    if (digits.All(c => c == '0'))
      throw new DomainException("CNIC 00000-0000000-0 is not a real CNIC.");

    if (digits[0] == '0')
      throw new DomainException("A CNIC cannot start with 0 - check the first digit.");

    return new Cnic(Format(digits));
  }

  public static Cnic? OfNullable(string? value) => string.IsNullOrWhiteSpace(value) ? null : Of(value);

  /// A value read back from the database: only reformatted, never refused, so rows saved before the
  /// leading-zero rule still load.
  public static Cnic FromStored(string value)
  {
    var digits = DigitsOf(value);
    return new Cnic(digits.Length == DigitCount && digits.All(char.IsAsciiDigit) ? Format(digits) : value);
  }

  /// For lookups: the dashed form of 13 digits written any way, or null when the text is not 13 digits.
  public static string? Normalize(string? value)
  {
    if (string.IsNullOrWhiteSpace(value))
      return null;

    var digits = DigitsOf(value);
    return digits.Length == DigitCount && digits.All(char.IsAsciiDigit) ? Format(digits) : null;
  }

  /// For "starts with" lookups: the first digits of a CNIC (at least 5) in the dashed form, e.g. 3520212 ->
  /// 35202-12. Null when the text is not 5 to 13 digits.
  public static string? NormalizePrefix(string? value, int minimumDigits = 5)
  {
    if (string.IsNullOrWhiteSpace(value))
      return null;

    var digits = DigitsOf(value);
    if (digits.Length < minimumDigits || digits.Length > DigitCount || !digits.All(char.IsAsciiDigit))
      return null;

    return digits.Length switch
    {
      <= 5 => digits,
      <= 12 => $"{digits[..5]}-{digits[5..]}",
      _ => Format(digits)
    };
  }

  internal static string DigitsOf(string value) => new(value.Where(c => c != '-' && !char.IsWhiteSpace(c)).ToArray());

  private static string Format(string digits) => $"{digits[..5]}-{digits[5..12]}-{digits[12]}";

  public override string ToString() => Value;
}
