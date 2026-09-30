/// Sq Ft (base), Sq M, Marla, Kanal ... Every area column stores the entered value plus a square-feet
/// copy computed with factor_to_base (schema guide, rule 7).
public sealed class MeasurementUnit : MasterData
{
  public decimal FactorToBase { get; private set; }
  public bool IsBase { get; private set; }

  public decimal ToBase(decimal value) => decimal.Round(value * FactorToBase, 4, MidpointRounding.AwayFromZero);

  protected override void ApplyExtras(MasterExtras extras)
  {
    var isBase = extras.IsBase ?? IsBase;
    var factor = extras.FactorToBase ?? (FactorToBase > 0 ? FactorToBase : (isBase ? 1m : 0m));

    if (factor <= 0)
      throw new DomainException("A measurement unit needs factor_to_base greater than zero (square feet per unit).");

    if (isBase && factor != 1m)
      throw new DomainException("The base unit (square feet) must have factor_to_base = 1.");

    FactorToBase = decimal.Round(factor, 8);
    IsBase = isBase;
  }
}
