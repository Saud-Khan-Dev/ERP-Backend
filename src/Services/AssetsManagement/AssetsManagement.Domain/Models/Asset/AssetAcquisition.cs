/// How and for how much the asset came to GDA - one record per asset (asset_id is unique). Its warranty dates are
/// THE warranty of the asset (reports, reminders). The cost is the base of every depreciation schedule.
public class AssetAcquisition : Aggregate<AssetAcquisitionId>
{
  /// Dates before this are typing mistakes (a year 1001), not real purchases or warranties.
  public static readonly DateOnly EarliestDate = new(1900, 1, 1);

  public AssetId AssetId { get; private set; } = default!;
  public DateOnly AcquisitionDate { get; private set; }
  public decimal AcquisitionCost { get; private set; }
  public Currency CurrencyCode { get; private set; } = default!;
  /// Rate to base currency at acquisition date.
  public decimal? ExchangeRate { get; private set; }
  /// acquisition_cost * exchange_rate
  public decimal? BaseCurrencyCost { get; private set; }
  /// Who it was bought from, as written on the invoice (a Procurement link comes later).
  public string? SupplierName { get; private set; }
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
      string? supplierName,
      string? purchaseReference,
      AcquisitionType acquisitionType,
      DateOnly? warrantyStartDate,
      DateOnly? warrantyExpiryDate,
      DateOnly today)
  {
    ArgumentNullException.ThrowIfNull(assetId);

    var acquisition = new AssetAcquisition { Id = id, AssetId = assetId };
    acquisition.Apply(acquisitionDate, acquisitionCost, currencyCode, exchangeRate, supplierName, purchaseReference, acquisitionType, warrantyStartDate, warrantyExpiryDate, today);
    return acquisition;
  }

  /// `depreciationPosted`: once a month of depreciation is posted the cost, the purchase date and the currency are the
  /// basis of the books and stay as they are. `scheduleActive`: a running schedule was worked out from this cost.
  public void Update(
      DateOnly acquisitionDate,
      decimal acquisitionCost,
      Currency currencyCode,
      decimal? exchangeRate,
      string? supplierName,
      string? purchaseReference,
      AcquisitionType acquisitionType,
      DateOnly? warrantyStartDate,
      DateOnly? warrantyExpiryDate,
      DateOnly today,
      bool depreciationPosted,
      bool scheduleActive)
  {
    ArgumentNullException.ThrowIfNull(currencyCode);

    if ((depreciationPosted || scheduleActive) && acquisitionCost != AcquisitionCost)
      throw new DomainException(depreciationPosted
        ? "The cost cannot change: depreciation has already been posted on it."
        : "The cost cannot change while a depreciation schedule worked out from it is running. Close the schedule first.");

    if (depreciationPosted && acquisitionDate != AcquisitionDate)
      throw new DomainException("The purchase date cannot change: depreciation has already been posted from it.");

    if (depreciationPosted && currencyCode != CurrencyCode)
      throw new DomainException("The currency cannot change: depreciation has already been posted in it.");

    Apply(acquisitionDate, acquisitionCost, currencyCode, exchangeRate, supplierName, purchaseReference, acquisitionType, warrantyStartDate, warrantyExpiryDate, today);
  }

  /// Only the warranty dates - e.g. a warranty card that arrives after the books were closed on the cost.
  public void SetWarranty(DateOnly? warrantyStartDate, DateOnly? warrantyExpiryDate)
  {
    CheckWarranty(AcquisitionDate, warrantyStartDate, warrantyExpiryDate);
    WarrantyStartDate = warrantyStartDate;
    WarrantyExpiryDate = warrantyExpiryDate;
  }

  private void Apply(
      DateOnly acquisitionDate,
      decimal acquisitionCost,
      Currency currencyCode,
      decimal? exchangeRate,
      string? supplierName,
      string? purchaseReference,
      AcquisitionType acquisitionType,
      DateOnly? warrantyStartDate,
      DateOnly? warrantyExpiryDate,
      DateOnly today)
  {
    ArgumentNullException.ThrowIfNull(currencyCode);

    if (acquisitionCost < 0)
      throw new DomainException("The cost cannot be negative.");

    if (exchangeRate is <= 0)
      throw new DomainException("The exchange rate must be greater than zero.");

    if (acquisitionDate < EarliestDate)
      throw new DomainException("The purchase date is not a real date. Check the year.");

    if (acquisitionDate > today)
      throw new DomainException("The purchase date cannot be in the future.");

    CheckWarranty(acquisitionDate, warrantyStartDate, warrantyExpiryDate);

    AcquisitionDate = acquisitionDate;
    AcquisitionCost = acquisitionCost;
    CurrencyCode = currencyCode;
    ExchangeRate = exchangeRate;
    BaseCurrencyCost = exchangeRate.HasValue ? Math.Round(acquisitionCost * exchangeRate.Value, 2, MidpointRounding.AwayFromZero) : null;
    SupplierName = string.IsNullOrWhiteSpace(supplierName) ? null : supplierName.Trim();
    PurchaseReference = string.IsNullOrWhiteSpace(purchaseReference) ? null : purchaseReference.Trim();
    AcquisitionType = acquisitionType;
    WarrantyStartDate = warrantyStartDate;
    WarrantyExpiryDate = warrantyExpiryDate;
  }

  private static void CheckWarranty(DateOnly acquisitionDate, DateOnly? warrantyStartDate, DateOnly? warrantyExpiryDate)
  {
    if (warrantyStartDate < EarliestDate || warrantyExpiryDate < EarliestDate)
      throw new DomainException("A warranty date is not a real date. Check the year.");

    if (warrantyExpiryDate.HasValue && warrantyExpiryDate < acquisitionDate)
      throw new DomainException("The warranty cannot end before the purchase date.");

    if (warrantyStartDate.HasValue && warrantyExpiryDate.HasValue && warrantyExpiryDate < warrantyStartDate)
      throw new DomainException("The warranty cannot end before it starts.");
  }
}
