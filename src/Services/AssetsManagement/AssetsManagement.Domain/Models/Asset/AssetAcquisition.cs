/// One acquisition record per asset (asset_id is unique).
public class AssetAcquisition : Aggregate<AssetAcquisitionId>
{
  public AssetId AssetId { get; private set; } = default!;
  public DateOnly AcquisitionDate { get; private set; }
  public decimal AcquisitionCost { get; private set; }
  public Currency CurrencyCode { get; private set; } = default!;
  /// Rate to base currency at acquisition date.
  public decimal? ExchangeRate { get; private set; }
  /// acquisition_cost * exchange_rate
  public decimal? BaseCurrencyCost { get; private set; }
  public Guid? SupplierId { get; private set; }
  /// PO / invoice / contract ID
  public string? PurchaseReference { get; private set; }
  public AcquisitionType AcquisitionType { get; private set; }
  public DateOnly? WarrantyStartDate { get; private set; }
  public DateOnly? WarrantyExpiryDate { get; private set; }

  public static AssetAcquisition Create(
      AssetAcquisitionId id,
      AssetId assetId,
      DateOnly acquisitionDate,
      decimal acquisitionCost,
      Currency currencyCode,
      decimal? exchangeRate,
      Guid? supplierId,
      string? purchaseReference,
      AcquisitionType acquisitionType,
      DateOnly? warrantyStartDate,
      DateOnly? warrantyExpiryDate)
  {
    ArgumentNullException.ThrowIfNull(assetId);

    var acquisition = new AssetAcquisition { Id = id, AssetId = assetId };
    acquisition.Apply(acquisitionDate, acquisitionCost, currencyCode, exchangeRate, supplierId, purchaseReference, acquisitionType, warrantyStartDate, warrantyExpiryDate);
    return acquisition;
  }

  public void Update(
      DateOnly acquisitionDate,
      decimal acquisitionCost,
      Currency currencyCode,
      decimal? exchangeRate,
      Guid? supplierId,
      string? purchaseReference,
      AcquisitionType acquisitionType,
      DateOnly? warrantyStartDate,
      DateOnly? warrantyExpiryDate)
  {
    Apply(acquisitionDate, acquisitionCost, currencyCode, exchangeRate, supplierId, purchaseReference, acquisitionType, warrantyStartDate, warrantyExpiryDate);
  }

  private void Apply(
      DateOnly acquisitionDate,
      decimal acquisitionCost,
      Currency currencyCode,
      decimal? exchangeRate,
      Guid? supplierId,
      string? purchaseReference,
      AcquisitionType acquisitionType,
      DateOnly? warrantyStartDate,
      DateOnly? warrantyExpiryDate)
  {
    ArgumentNullException.ThrowIfNull(currencyCode);

    if (acquisitionCost < 0)
      throw new DomainException("Acquisition cost cannot be negative.");

    if (exchangeRate is <= 0)
      throw new DomainException("Exchange rate must be greater than zero.");

    if (warrantyExpiryDate.HasValue && warrantyExpiryDate < acquisitionDate)
      throw new DomainException("Warranty expiry date cannot be before the acquisition date.");

    if (warrantyStartDate.HasValue && warrantyExpiryDate.HasValue && warrantyExpiryDate < warrantyStartDate)
      throw new DomainException("Warranty expiry date cannot be before the warranty start date.");

    AcquisitionDate = acquisitionDate;
    AcquisitionCost = acquisitionCost;
    CurrencyCode = currencyCode;
    ExchangeRate = exchangeRate;
    BaseCurrencyCost = exchangeRate.HasValue ? Math.Round(acquisitionCost * exchangeRate.Value, 2, MidpointRounding.AwayFromZero) : null;
    SupplierId = supplierId;
    PurchaseReference = string.IsNullOrWhiteSpace(purchaseReference) ? null : purchaseReference.Trim();
    AcquisitionType = acquisitionType;
    WarrantyStartDate = warrantyStartDate;
    WarrantyExpiryDate = warrantyExpiryDate;
  }
}
