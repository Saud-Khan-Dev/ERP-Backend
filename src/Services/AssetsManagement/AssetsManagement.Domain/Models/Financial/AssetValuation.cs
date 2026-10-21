/// asset_valuation_history: what the asset was found to be worth on a date (an appraisal, an insurance assessment) -
/// one per asset per date. It is kept as the asset's value history; the book value does not change with it.
public class AssetValuation : Aggregate<AssetValuationId>
{
  public AssetId AssetId { get; private set; } = default!;
  public DateOnly ValuationDate { get; private set; }
  public decimal Value { get; private set; }
  public Currency CurrencyCode { get; private set; } = default!;
  public string? ValuationMethod { get; private set; }
  /// Who valued it (an appraiser, a committee) as written on the report.
  public string? ValuedByName { get; private set; }
  public string? Notes { get; private set; }
  /// Who recorded it (the signed-in user).
  public Guid? RecordedBy { get; private set; }
  public string? RecordedByName { get; private set; }

  public static AssetValuation Create(
      AssetValuationId id,
      AssetId assetId,
      DateOnly valuationDate,
      decimal value,
      Currency currencyCode,
      string? valuationMethod,
      string? valuedByName,
      string? notes,
      ActorRef recordedBy,
      DateOnly today)
  {
    ArgumentNullException.ThrowIfNull(assetId);
    ArgumentNullException.ThrowIfNull(currencyCode);
    ArgumentNullException.ThrowIfNull(recordedBy);

    if (value < 0)
      throw new DomainException("A value cannot be negative.");

    if (valuationDate < AssetAcquisition.EarliestDate)
      throw new DomainException("The valuation date is not a real date. Check the year.");

    if (valuationDate > today)
      throw new DomainException("A valuation cannot be dated in the future.");

    return new AssetValuation
    {
      Id = id,
      AssetId = assetId,
      ValuationDate = valuationDate,
      Value = value,
      CurrencyCode = currencyCode,
      ValuationMethod = string.IsNullOrWhiteSpace(valuationMethod) ? null : valuationMethod.Trim(),
      ValuedByName = string.IsNullOrWhiteSpace(valuedByName) ? null : valuedByName.Trim(),
      Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
      RecordedBy = recordedBy.Id,
      RecordedByName = recordedBy.ShortName
    };
  }
}
