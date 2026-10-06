/// Pakistani IBAN: PK + 2 check digits + 4-letter bank code + 16 digits (24 characters), stored without spaces.
public static class Iban
{
  public const int Length = 24;

  public static string? NormalizeOptional(string? value)
  {
    if (string.IsNullOrWhiteSpace(value))
      return null;

    var iban = new string(value.Where(c => !char.IsWhiteSpace(c) && c != '-').ToArray()).ToUpperInvariant();

    var valid = iban.Length == Length
      && iban.StartsWith("PK", StringComparison.Ordinal)
      && iban[2..4].All(char.IsAsciiDigit)
      && iban[4..8].All(char.IsAsciiLetterUpper)
      && iban[8..].All(char.IsAsciiDigit);

    if (!valid)
      throw new DomainException("IBAN must be PK, 2 digits, the 4-letter bank code and 16 digits (e.g. PK36SCBL0000001123456702).");

    if (!HasValidCheckDigits(iban))
      throw new DomainException("The IBAN's check digits do not match. Copy it again from the bank's letter.");

    return iban;
  }

  /// ISO 13616 mod-97 check.
  private static bool HasValidCheckDigits(string iban)
  {
    var rearranged = iban[4..] + iban[..4];
    var remainder = 0;

    foreach (var c in rearranged)
    {
      var digits = char.IsAsciiDigit(c) ? (c - '0').ToString() : (c - 'A' + 10).ToString();
      foreach (var d in digits)
        remainder = (remainder * 10 + (d - '0')) % 97;
    }

    return remainder == 1;
  }
}
