/// Pakistani CNIC. Accepts 13 digits with or without dashes; stored as 12345-1234567-1.
public sealed record Cnic
{
  public const int MaxLength = 15;

  public string Value { get; }

  private Cnic(string value) => Value = value;

  public static Cnic Of(string value)
  {
    if (string.IsNullOrWhiteSpace(value))
      throw new DomainException("CNIC is required.");

    var digits = new string(value.Where(c => c != '-' && !char.IsWhiteSpace(c)).ToArray());

    if (digits.Length != 13 || !digits.All(char.IsAsciiDigit))
      throw new DomainException("CNIC must have 13 digits (e.g. 12345-1234567-1).");

    return new Cnic($"{digits[..5]}-{digits[5..12]}-{digits[12]}");
  }

  public static Cnic? OfNullable(string? value) => string.IsNullOrWhiteSpace(value) ? null : Of(value);

  public override string ToString() => Value;
}
