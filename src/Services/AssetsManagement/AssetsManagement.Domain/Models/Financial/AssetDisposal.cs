/// One disposal per asset (asset_id is unique).
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
  public Guid? ApprovedBy { get; private set; }
  public DateTime? ApprovedAt { get; private set; }

  public static AssetDisposal Create(
      AssetDisposalId id,
      AssetId assetId,
      DisposalMethod method,
      DateOnly disposalDate,
      decimal? disposalValue,
      Currency? currencyCode,
      decimal? netBookValueAtDisposal,
      string? buyerInfo,
      string? reason,
      Guid? approvedBy,
      DateTime? approvedAt)
  {
    ArgumentNullException.ThrowIfNull(assetId);
    ArgumentNullException.ThrowIfNull(method);

    if (!method.IsActive)
      throw new DomainException($"Disposal method '{method.Name.Value}' is inactive.");

    if (method.RequiresValue && disposalValue is null)
      throw new DomainException($"Disposal method '{method.Name.Value}' requires a disposal value.");

    if (disposalValue is < 0)
      throw new DomainException("Disposal value cannot be negative.");

    if (disposalValue is not null && currencyCode is null)
      throw new DomainException("A currency is required when a disposal value is recorded.");

    return new AssetDisposal
    {
      Id = id,
      AssetId = assetId,
      DisposalMethodId = method.Id,
      DisposalDate = disposalDate,
      DisposalValue = disposalValue,
      CurrencyCode = currencyCode,
      NetBookValueAtDisposal = netBookValueAtDisposal,
      GainLoss = disposalValue.HasValue && netBookValueAtDisposal.HasValue ? disposalValue - netBookValueAtDisposal : null,
      BuyerInfo = buyerInfo,
      Reason = reason,
      ApprovedBy = approvedBy,
      ApprovedAt = approvedAt ?? (approvedBy is null ? null : DateTime.UtcNow)
    };
  }
}
