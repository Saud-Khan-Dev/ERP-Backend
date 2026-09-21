public class AssetDepreciationEntry : Entity<AssetDepreciationEntryId>
{
  public AssetDepreciationScheduleId ScheduleId { get; private set; } = default!;
  public DateOnly PeriodStart { get; private set; }
  public DateOnly PeriodEnd { get; private set; }
  public decimal OpeningBookValue { get; private set; }
  public decimal DepreciationAmount { get; private set; }
  public decimal AccumulatedDepreciation { get; private set; }
  public decimal BookValueAfter { get; private set; }
  public bool Posted { get; private set; }
  public DateTime? PostedAt { get; private set; }
  public Guid? PostedBy { get; private set; }
  public DateTime? ReversedAt { get; private set; }
  public Guid? ReversedBy { get; private set; }

  public bool IsReversed => ReversedAt.HasValue;

  internal static AssetDepreciationEntry Create(
      AssetDepreciationScheduleId scheduleId,
      DateOnly periodStart,
      DateOnly periodEnd,
      decimal openingBookValue,
      decimal depreciationAmount,
      decimal accumulatedDepreciation)
  {
    if (periodEnd <= periodStart)
      throw new DomainException("period_end must be after period_start.");

    if (depreciationAmount < 0)
      throw new DomainException("Depreciation amount cannot be negative.");

    if (depreciationAmount > openingBookValue)
      throw new DomainException("Depreciation amount cannot exceed the opening book value.");

    return new AssetDepreciationEntry
    {
      Id = AssetDepreciationEntryId.Of(Guid.NewGuid()),
      ScheduleId = scheduleId,
      PeriodStart = periodStart,
      PeriodEnd = periodEnd,
      OpeningBookValue = openingBookValue,
      DepreciationAmount = depreciationAmount,
      AccumulatedDepreciation = accumulatedDepreciation,
      BookValueAfter = openingBookValue - depreciationAmount,
      Posted = false
    };
  }

  internal void Post(Guid? postedBy, DateTime postedAt)
  {
    if (IsReversed)
      throw new DomainException("A reversed entry cannot be posted.");

    if (Posted)
      throw new DomainException("This entry has already been posted.");

    Posted = true;
    PostedAt = postedAt;
    PostedBy = postedBy;
  }

  internal void Reverse(Guid? reversedBy, DateTime reversedAt)
  {
    if (!Posted)
      throw new DomainException("Only posted entries can be reversed. Delete unposted entries instead.");

    if (IsReversed)
      throw new DomainException("This entry has already been reversed.");

    ReversedAt = reversedAt;
    ReversedBy = reversedBy;
  }
}
