/// asset_valuation_history: one valuation per asset per date.
public class AssetValuation : Aggregate<AssetValuationId>
{
  public AssetId AssetId { get; private set; } = default!;
  public DateOnly ValuationDate { get; private set; }
  public decimal Value { get; private set; }
  public Currency CurrencyCode { get; private set; } = default!;
  public string? ValuationMethod { get; private set; }
  public Guid? ValuedBy { get; private set; }
  public string? Notes { get; private set; }

  public static AssetValuation Create(
      AssetValuationId id,
      AssetId assetId,
      DateOnly valuationDate,
      decimal value,
      Currency currencyCode,
      string? valuationMethod,
      Guid? valuedBy,
      string? notes)
  {
    ArgumentNullException.ThrowIfNull(assetId);
    ArgumentNullException.ThrowIfNull(currencyCode);

    if (value < 0)
      throw new DomainException("Valuation value cannot be negative.");

    return new AssetValuation
    {
      Id = id,
      AssetId = assetId,
      ValuationDate = valuationDate,
      Value = value,
      CurrencyCode = currencyCode,
      ValuationMethod = string.IsNullOrWhiteSpace(valuationMethod) ? null : valuationMethod.Trim(),
      ValuedBy = valuedBy,
      Notes = notes
    };
  }
}
