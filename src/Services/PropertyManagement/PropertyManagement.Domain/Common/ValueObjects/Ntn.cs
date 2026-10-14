/// FBR National Tax Number. Two shapes are issued: the classic 7 digits + check digit (1234567-8) and, since
/// FBR moved individuals onto their CNIC, the 13-digit CNIC-based number (12345-1234567-1). Either may be typed
/// with or without dashes and spaces; it is stored in the dashed form, so two NTNs are the same when their
/// digits are.
public sealed record Ntn
{
  public const int MaxLength = 20;

  public string Value { get; }

  private Ntn(string value) => Value = value;

  public static Ntn Of(string value)
  {
    if (string.IsNullOrWhiteSpace(value))
      throw new DomainException("NTN is required.");

    return Normalize(value) is { } normalized
      ? new Ntn(normalized)
      : throw new DomainException("NTN must be 7 digits and a check digit (1234567-8) or a 13-digit CNIC-based number (12345-1234567-1).");
  }

  public static Ntn? OfNullable(string? value) => string.IsNullOrWhiteSpace(value) ? null : Of(value);

  /// The dashed form of a valid NTN written any way, or null when it is not one.
  public static string? Normalize(string? value)
  {
    if (string.IsNullOrWhiteSpace(value))
      return null;

    var digits = Cnic.DigitsOf(value.Trim());
    if (!digits.All(char.IsAsciiDigit) || digits.All(c => c == '0'))
      return null;

    return digits.Length switch
    {
      8 => $"{digits[..7]}-{digits[7]}",
      Cnic.DigitCount when digits[0] != '0' => $"{digits[..5]}-{digits[5..12]}-{digits[12]}",
      _ => null
    };
  }

  /// The digits only: what two NTNs are compared on.
  public static string Digits(string value) => new(value.Where(char.IsAsciiDigit).ToArray());

  public override string ToString() => Value;
}
