/// ISO 4217 currency table (the `currency` table). Keyed by the existing Currency value object so
/// acquisition / valuation / disposal rows can FK to it. Named CurrencyLookup to avoid clashing with the value object.
public class CurrencyLookup : Entity<Currency>
{
  public Name Name { get; private set; } = default!;
  public string? Symbol { get; private set; }
  /// Number of decimal places, drives rounding rules (2 for USD, 0 for JPY, 3 for KWD).
  public short MinorUnits { get; private set; }
  public bool IsActive { get; private set; }

  public static CurrencyLookup Create(Currency code, Name name, string? symbol, short minorUnits)
  {
    ArgumentNullException.ThrowIfNull(code);
    ArgumentNullException.ThrowIfNull(name);
    ValidateMinorUnits(minorUnits);

    return new CurrencyLookup
    {
      Id = code,
      Name = name,
      Symbol = symbol,
      MinorUnits = minorUnits,
      IsActive = true
    };
  }

  public void Update(Name name, string? symbol, short minorUnits, bool isActive)
  {
    ArgumentNullException.ThrowIfNull(name);
    ValidateMinorUnits(minorUnits);

    Name = name;
    Symbol = symbol;
    MinorUnits = minorUnits;
    IsActive = isActive;
  }

  private static void ValidateMinorUnits(short minorUnits)
  {
    if (minorUnits is < 0 or > 6)
      throw new DomainException("minor_units must be between 0 and 6.");
  }
}
