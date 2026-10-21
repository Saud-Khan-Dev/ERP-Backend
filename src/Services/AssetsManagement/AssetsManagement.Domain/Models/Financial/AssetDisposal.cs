/// How an asset left GDA - one disposal per asset (asset_id is unique). The book value on the disposal date is kept
/// with it, and the gain or loss is worked out against it: (proceeds, or nothing) minus that book value.
public class AssetDisposal : Aggregate<AssetDisposalId>
{
  public AssetId AssetId { get; private set; } = default!;
  public DateOnly DisposalDate { get; private set; }
  public DisposalMethodId DisposalMethodId { get; private set; } = default!;
  public decimal? DisposalValue { get; private set; }
  public Currency? CurrencyCode { get; private set; }
  /// Snapshot, to compute gain/loss.
  public decimal? NetBookValueAtDisposal { get; private set; }
  public decimal? GainLoss { get; private set; }
  public string? BuyerInfo { get; private set; }
  public string? Reason { get; private set; }
  /// Recording the disposal is the approval (ASSET_FINANCE.APPROVE): who did it and when.
  public Guid? ApprovedBy { get; private set; }
  public string? ApprovedByName { get; private set; }
  public DateTime? ApprovedAt { get; private set; }

  public static AssetDisposal Create(
      AssetDisposalId id,
      AssetId assetId,
      DisposalMethod method,
      DateOnly disposalDate,
      decimal? disposalValue,
      Currency? currencyCode,
      Currency? purchaseCurrency,
      DateOnly? purchaseDate,
      decimal? netBookValueAtDisposal,
      string? buyerInfo,
      string? reason,
      ActorRef approvedBy,
      DateTime now,
      DateOnly today)
  {
    ArgumentNullException.ThrowIfNull(assetId);
    ArgumentNullException.ThrowIfNull(method);
    ArgumentNullException.ThrowIfNull(approvedBy);

    if (!method.IsActive)
      throw new DomainException($"The disposal method '{method.Name.Value}' is switched off.");

    if (disposalDate < AssetAcquisition.EarliestDate)
      throw new DomainException("The disposal date is not a real date. Check the year.");

    if (disposalDate > today)
      throw new DomainException("The disposal date cannot be in the future.");

    if (purchaseDate.HasValue && disposalDate < purchaseDate.Value)
      throw new DomainException($"The disposal date cannot be before the purchase date ({purchaseDate.Value:d MMM yyyy}).");

    if (method.RequiresValue && disposalValue is null)
      throw new DomainException($"'{method.Name.Value}' records what was received: enter the proceeds.");

    if (!method.AllowsValue && disposalValue is not null)
      throw new DomainException($"'{method.Name.Value}' records no proceeds. Leave the proceeds empty, or choose another method.");

    if (disposalValue is < 0)
      throw new DomainException("The proceeds cannot be negative.");

    if (disposalValue is not null && currencyCode is null)
      throw new DomainException("Choose the currency of the proceeds.");

    if (disposalValue is not null && purchaseCurrency is not null && currencyCode != purchaseCurrency)
      throw new DomainException($"Record the proceeds in {purchaseCurrency.Value}, the currency the asset was bought in - the gain or loss is worked out against its book value.");

    return new AssetDisposal
    {
      Id = id,
      AssetId = assetId,
      DisposalMethodId = method.Id,
      DisposalDate = disposalDate,
      DisposalValue = disposalValue,
      CurrencyCode = disposalValue is null ? null : currencyCode,
      NetBookValueAtDisposal = netBookValueAtDisposal,
      // a write-off with no proceeds loses the whole book value
      GainLoss = netBookValueAtDisposal.HasValue ? (disposalValue ?? 0m) - netBookValueAtDisposal.Value : null,
      BuyerInfo = string.IsNullOrWhiteSpace(buyerInfo) ? null : buyerInfo.Trim(),
      Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
      ApprovedBy = approvedBy.Id,
      ApprovedByName = approvedBy.ShortName,
      ApprovedAt = now
    };
  }
}
