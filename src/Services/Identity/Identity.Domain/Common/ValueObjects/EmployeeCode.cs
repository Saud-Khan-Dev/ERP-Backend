using System.Text.RegularExpressions;

/// Human-readable employee number shown on the account, e.g. EMP-101 - HR's number for the person (HRM issues it;
/// the account takes it when it is linked to the HR record). Only the character set is checked here.
public sealed record EmployeeCode
{
  public const int MaxLength = 30;

  public string Value { get; }

  private static readonly Regex Pattern = new(@"^[A-Z0-9][A-Z0-9\-_/]*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

  private EmployeeCode(string value) => Value = value;

  public static EmployeeCode Of(string value)
  {
    if (string.IsNullOrWhiteSpace(value))
      throw new DomainException("Employee code is required.");

    value = value.Trim().ToUpperInvariant();

    if (value.Length > MaxLength)
      throw new DomainException($"Employee code cannot exceed {MaxLength} characters.");

    if (!Pattern.IsMatch(value))
      throw new DomainException("Employee code may only contain letters, digits, '-', '_' and '/' (e.g. EMP-101).");

    return new EmployeeCode(value);
  }

  public override string ToString() => Value;
}
