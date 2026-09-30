using System.Globalization;
using System.Text.RegularExpressions;

/// One admin-editable numbering scheme per kind of record (schema guide, rule 8):
///
///   PROPERTY  → PROP-00001      OWNER → OWN-00001      TRANSFER → TRF-00001
///
/// Users never type these codes; they are issued here. Changing a sequence only affects codes issued
/// afterwards — existing codes are never rewritten, and property_code in particular never changes.
public class CodeSequence : Aggregate<CodeSequenceId>
{
  public const int MaxPrefixLength = 10;
  public const int MaxMinimumDigits = 10;
  public const long MaxNumber = 999_999_999_999;

  private static readonly string[] AllowedSeparators = { "", "-", "_", "/" };
  private static readonly Regex PrefixPattern = new(@"^[A-Z][A-Z0-9]*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

  /// Which records this sequence numbers: PROPERTY, OWNER, TRANSFER ...
  public MasterCode Key { get; private set; } = default!;
  public string Prefix { get; private set; } = default!;
  public string Separator { get; private set; } = default!;
  public int MinimumDigits { get; private set; }
  public long NextNumber { get; private set; }

  /// "PROP-#####" — how the sequence reads in an admin screen.
  public string Pattern => $"{Prefix}{Separator}{new string('#', MinimumDigits)}";

  public BusinessCode NextCode => Format(NextNumber);

  public static CodeSequence Create(CodeSequenceId id, MasterCode key, string prefix, string? separator, int minimumDigits, long nextNumber)
  {
    ArgumentNullException.ThrowIfNull(key);

    var sequence = new CodeSequence { Id = id, Key = key };
    sequence.Apply(prefix, separator, minimumDigits, nextNumber);
    return sequence;
  }

  public void Update(string prefix, string? separator, int minimumDigits, long nextNumber) =>
      Apply(prefix, separator, minimumDigits, nextNumber);

  public BusinessCode Format(long number)
  {
    if (number is < 1 or > MaxNumber)
      throw new DomainException($"Sequence number must be between 1 and {MaxNumber}.");

    var digits = number.ToString(CultureInfo.InvariantCulture).PadLeft(MinimumDigits, '0');
    return BusinessCode.Of($"{Prefix}{Separator}{digits}");
  }

  /// Reads the number out of a code in this sequence's canonical format (PROP-00012 → 12).
  public bool TryReadNumber(BusinessCode code, out long number)
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

  /// Hands out the next code and moves the counter on. The caller skips any code already in use.
  public BusinessCode Issue()
  {
    var code = NextCode;
    NextNumber++;
    return code;
  }

  private void Apply(string prefix, string? separator, int minimumDigits, long nextNumber)
  {
    if (string.IsNullOrWhiteSpace(prefix))
      throw new DomainException("Prefix is required.");

    prefix = prefix.Trim().ToUpperInvariant();
    separator ??= string.Empty;

    if (prefix.Length > MaxPrefixLength)
      throw new DomainException($"Prefix cannot exceed {MaxPrefixLength} characters.");

    if (!PrefixPattern.IsMatch(prefix))
      throw new DomainException("Prefix must start with a letter and contain only letters and digits (e.g. PROP).");

    if (!AllowedSeparators.Contains(separator, StringComparer.Ordinal))
      throw new DomainException("Separator must be one of '-', '_', '/' or empty.");

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

/// The sequences this module issues, with the defaults they are seeded with.
public static class CodeSequenceKeys
{
  public const string Property = "PROPERTY";
  public const string Owner = "OWNER";
  public const string Transfer = "TRANSFER";

  public sealed record Default(string Key, string Prefix, string Separator, int MinimumDigits);

  public static IReadOnlyList<Default> Defaults { get; } = new[]
  {
    new Default(Property, "PROP", "-", 5),
    new Default(Owner, "OWN", "-", 5),
    new Default(Transfer, "TRF", "-", 5),
  };
}
