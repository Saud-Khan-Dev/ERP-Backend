/// Pakistani CNIC. Accepts 13 digits with or without dashes; stored as 35202-1234567-1 so the unique index is meaningful.
public static class Cnic
{
  public const int MaxLength = 15;

  public static string Normalize(string? value, string field = "CNIC")
  {
    if (string.IsNullOrWhiteSpace(value))
      throw new DomainException($"{field} is required.");

    var digits = new string(value.Where(c => c != '-' && !char.IsWhiteSpace(c)).ToArray());

    if (digits.Length != 13 || !digits.All(char.IsAsciiDigit))
      throw new DomainException($"{field} must have 13 digits (e.g. 35202-1234567-1).");

    return $"{digits[..5]}-{digits[5..12]}-{digits[12]}";
  }

  public static string? NormalizeOptional(string? value, string field = "CNIC") =>
      string.IsNullOrWhiteSpace(value) ? null : Normalize(value, field);
}
