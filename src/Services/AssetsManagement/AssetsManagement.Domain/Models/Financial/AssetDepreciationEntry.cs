/// One period of a depreciation schedule (a calendar month, or the part of one at the start or the end of the asset's
/// life). PeriodEnd is the first day AFTER the period - LastDay is the period's own last day.
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
  public string? PostedByName { get; private set; }
  public DateTime? ReversedAt { get; private set; }
  public Guid? ReversedBy { get; private set; }
  public string? ReversedByName { get; private set; }
  /// Why a posted month was reversed (asked since 2026-10-21; empty on older reversals).
  public string? ReversalReason { get; private set; }

  public bool IsReversed => ReversedAt.HasValue;

  /// The last day the period covers.
  public DateOnly LastDay => PeriodEnd.AddDays(-1);

  /// Posted, not reversed, and its period over by `date`: written off the asset's value by then.
  public bool WrittenOffBy(DateOnly date) => Posted && !IsReversed && LastDay <= date;

  internal static AssetDepreciationEntry Create(
      AssetDepreciationScheduleId scheduleId,
      DateOnly periodStart,
      DateOnly periodEnd,
      decimal openingBookValue,
      decimal depreciationAmount,
      decimal accumulatedDepreciation)
  {
    if (periodEnd <= periodStart)
      throw new DomainException("A depreciation period must end after it starts.");

    if (depreciationAmount < 0)
      throw new DomainException("A depreciation amount cannot be negative.");

    if (depreciationAmount > openingBookValue)
      throw new DomainException("A month's depreciation cannot be more than the book value at its start.");

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

  /// A month is posted (made final) once it is over - its last day may be today.
  internal void Post(ActorRef postedBy, DateTime postedAt, DateOnly today)
  {
    ArgumentNullException.ThrowIfNull(postedBy);

    if (IsReversed)
      throw new DomainException("A reversed month cannot be posted again.");

    if (Posted)
      throw new DomainException("This month is already posted.");

    if (LastDay > today)
      throw new DomainException($"The period {PeriodStart:dd'/'MM'/'yyyy} - {LastDay:dd'/'MM'/'yyyy} has not ended yet. A month is posted once it is over.");

    Posted = true;
    PostedAt = postedAt;
    PostedBy = postedBy.Id;
    PostedByName = postedBy.ShortName;
  }

  internal void Reverse(ActorRef reversedBy, DateTime reversedAt, string? reason)
  {
    ArgumentNullException.ThrowIfNull(reversedBy);

    if (!Posted)
      throw new DomainException("Only a posted month can be reversed. A draft month is deleted instead.");

    if (IsReversed)
      throw new DomainException("This month has already been reversed.");

    ReversedAt = reversedAt;
    ReversedBy = reversedBy.Id;
    ReversedByName = reversedBy.ShortName;
    ReversalReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
  }
}
