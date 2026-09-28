using System.Globalization;
using System.Text.RegularExpressions;

/// The admin-editable numbering scheme for employee codes:
///
///   prefix "EMP" + separator "-" + number padded to 3 digits  →  EMP-001 … EMP-101 … EMP-1000
///
/// One row for the whole system. Changing it only affects codes issued or edited afterwards;
/// codes already on accounts are never rewritten.
public class EmployeeCodeTemplate : Aggregate<EmployeeCodeTemplateId>
{
  /// Fixed id so the table can only ever hold the one template.
  public static readonly Guid SingletonId = new("7b1e4f2a-0c5d-4e8b-9a61-3f2d8c4b5e01");

  public const string DefaultPrefix = "EMP";
  public const string DefaultSeparator = "-";
  public const int DefaultMinimumDigits = 3;
  public const long DefaultNextNumber = 1;

  public const int MaxPrefixLength = 10;
  public const int MaxMinimumDigits = 10;
  public const long MaxNumber = 999_999_999_999;

  private static readonly string[] AllowedSeparators = { "", "-", "_", "/" };
  private static readonly Regex PrefixPattern = new(@"^[A-Z][A-Z0-9]*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

  public string Prefix { get; private set; } = default!;
  public string Separator { get; private set; } = default!;
  /// Numbers are left-padded with zeros to at least this many digits; larger numbers simply grow.
  public int MinimumDigits { get; private set; }
  /// The number the next auto-generated code will use.
  public long NextNumber { get; private set; }

  /// "EMP-###" — how the template reads in an admin screen.
  public string Pattern => $"{Prefix}{Separator}{new string('#', MinimumDigits)}";

  /// The code the next auto-generated account would receive.
  public EmployeeCode NextCode => Format(NextNumber);

  public static EmployeeCodeTemplate CreateDefault() =>
      Create(EmployeeCodeTemplateId.Of(SingletonId), DefaultPrefix, DefaultSeparator, DefaultMinimumDigits, DefaultNextNumber);

  public static EmployeeCodeTemplate Create(
      EmployeeCodeTemplateId id,
      string prefix,
      string? separator,
      int minimumDigits,
      long nextNumber)
  {
    var template = new EmployeeCodeTemplate { Id = id };
    template.Apply(prefix, separator, minimumDigits, nextNumber);
    return template;
  }

  public void Update(string prefix, string? separator, int minimumDigits, long nextNumber) =>
      Apply(prefix, separator, minimumDigits, nextNumber);

  public EmployeeCode Format(long number)
  {
    if (number is < 1 or > MaxNumber)
      throw new DomainException($"Employee number must be between 1 and {MaxNumber}.");

    var digits = number.ToString(CultureInfo.InvariantCulture).PadLeft(MinimumDigits, '0');
    return EmployeeCode.Of($"{Prefix}{Separator}{digits}");
  }

  /// Reads the number out of a code written in this template's format. Only the canonical spelling
  /// counts, so with 3 digits "EMP-101" matches but "EMP-0101" and "EMP-01" do not.
  public bool TryReadNumber(EmployeeCode code, out long number)
  {
    ArgumentNullException.ThrowIfNull(code);
    number = 0;

    var head = Prefix + Separator;
    if (!code.Value.StartsWith(head, StringComparison.Ordinal))
      return false;

    var digits = code.Value[head.Length..];
    if (digits.Length == 0 || !digits.All(char.IsAsciiDigit))
      return false;

    if (!long.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out number) || number is < 1 or > MaxNumber)
      return false;

    return Format(number) == code;
  }

  public void EnsureMatches(EmployeeCode code)
  {
    if (!TryReadNumber(code, out _))
      throw new DomainException($"Employee code '{code.Value}' does not follow the template {Pattern} (e.g. {NextCode.Value}).");
  }

  /// Hands out the next code and moves the counter on. The caller is responsible for skipping a
  /// code that is somehow already in use (see EmployeeCodeService).
  public EmployeeCode Issue()
  {
    var code = NextCode;
    NextNumber++;
    return code;
  }

  /// A manually entered code in this format must never be handed out again by <see cref="Issue"/>.
  public void Reserve(EmployeeCode code)
  {
    if (TryReadNumber(code, out var number) && number >= NextNumber)
      NextNumber = number + 1;
  }

  private void Apply(string prefix, string? separator, int minimumDigits, long nextNumber)
  {
    if (string.IsNullOrWhiteSpace(prefix))
      throw new DomainException("Employee code prefix is required.");

    prefix = prefix.Trim().ToUpperInvariant();
    separator ??= string.Empty;

    if (prefix.Length > MaxPrefixLength)
      throw new DomainException($"Employee code prefix cannot exceed {MaxPrefixLength} characters.");

    if (!PrefixPattern.IsMatch(prefix))
      throw new DomainException("Employee code prefix must start with a letter and contain only letters and digits (e.g. EMP).");

    if (!AllowedSeparators.Contains(separator, StringComparer.Ordinal))
      throw new DomainException("Employee code separator must be one of '-', '_', '/' or empty.");

    if (minimumDigits is < 1 or > MaxMinimumDigits)
      throw new DomainException($"Minimum digits must be between 1 and {MaxMinimumDigits}.");

    if (nextNumber is < 1 or > MaxNumber)
      throw new DomainException($"Next number must be between 1 and {MaxNumber}.");

    Prefix = prefix;
    Separator = separator;
    MinimumDigits = minimumDigits;
    NextNumber = nextNumber;
  }
}
