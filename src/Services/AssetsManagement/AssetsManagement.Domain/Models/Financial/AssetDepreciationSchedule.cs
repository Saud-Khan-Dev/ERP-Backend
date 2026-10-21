/// How an asset's value is written off over its useful life. Only one live (is_active) schedule per asset - partial
/// unique index in the DB. A schedule starts from the book value the asset has on its start date (the cost for the first
/// one; what is left after the posted months for a revision), so writing off never goes past the cost.
///
/// Periods are calendar months: the first runs from the start date to the end of its month and the last from the 1st
/// to the end of the life, each a share of a full month by its days; the last one absorbs any rounding.
public class AssetDepreciationSchedule : Aggregate<AssetDepreciationScheduleId>
{
  private const decimal AverageDaysPerMonth = 30.436875m;

  private readonly List<AssetDepreciationEntry> _entries = new();

  public AssetId AssetId { get; private set; } = default!;
  public DepreciationMethodId MethodId { get; private set; } = default!;
  public int UsefulLifeMonths { get; private set; }
  public decimal SalvageValue { get; private set; }
  /// The book value the schedule starts from (cost, or what was left when it was revised).
  public decimal OpeningBookValue { get; private set; }
  /// Opening book value minus salvage: what the schedule writes off over its life.
  public decimal DepreciableBase { get; private set; }
  /// Required for declining balance (yearly rate, e.g. 0.20).
  public decimal? DecliningRate { get; private set; }
  public DateOnly StartDate { get; private set; }
  /// The first day after the schedule's life (or after its last month, once it was closed).
  public DateOnly? EndDate { get; private set; }
  public bool IsActive { get; private set; }
  public IReadOnlyList<AssetDepreciationEntry> Entries => _entries.AsReadOnly();

  /// The live (not reversed) months, oldest first.
  public IEnumerable<AssetDepreciationEntry> LiveEntries => _entries.Where(e => !e.IsReversed).OrderBy(e => e.PeriodStart);

  /// The last day of the last posted, not reversed month - null when nothing is posted.
  public DateOnly? PostedThrough => _entries.Where(e => e.Posted && !e.IsReversed).Select(e => (DateOnly?)e.LastDay).Max();

  public bool HasPostedEntries => _entries.Any(e => e.Posted && !e.IsReversed);

  public static AssetDepreciationSchedule Create(
      AssetDepreciationScheduleId id,
      Asset asset,
      AssetType assetType,
      DepreciationMethod method,
      decimal openingBookValue,
      int usefulLifeMonths,
      decimal salvageValue,
      decimal? decliningRate,
      DateOnly startDate)
  {
    ArgumentNullException.ThrowIfNull(asset);
    ArgumentNullException.ThrowIfNull(assetType);
    ArgumentNullException.ThrowIfNull(method);

    if (!assetType.IsDepreciable)
      throw new DomainException($"Assets of type '{assetType.Name.Value}' do not depreciate.");

    if (!method.IsActive)
      throw new DomainException($"The depreciation method '{method.Name.Value}' is switched off.");

    if (usefulLifeMonths <= 0)
      throw new DomainException("The useful life must be at least one month.");

    if (usefulLifeMonths > 1200)
      throw new DomainException("The useful life cannot be more than 100 years.");

    if (startDate < AssetAcquisition.EarliestDate)
      throw new DomainException("The start date is not a real date. Check the year.");

    if (salvageValue < 0)
      throw new DomainException("The value at the end (salvage) cannot be negative.");

    if (openingBookValue <= 0)
      throw new DomainException("There is nothing to depreciate: the book value is zero.");

    if (salvageValue > openingBookValue)
      throw new DomainException("The value at the end (salvage) cannot be more than the book value it starts from.");

    if (method.Kind == DepreciationMethodKind.DecliningBalance && decliningRate is null or <= 0 or > 1)
      throw new DomainException("Declining balance needs a yearly rate between 0 % and 100 %.");

    return new AssetDepreciationSchedule
    {
      Id = id,
      AssetId = asset.Id,
      MethodId = method.Id,
      UsefulLifeMonths = usefulLifeMonths,
      SalvageValue = salvageValue,
      OpeningBookValue = openingBookValue,
      DepreciableBase = openingBookValue - salvageValue,
      DecliningRate = method.Kind == DepreciationMethodKind.DecliningBalance ? decliningRate : null,
      StartDate = startDate,
      EndDate = startDate.AddMonths(usefulLifeMonths),
      IsActive = true
    };
  }

  /// The book value after the posted months that ended on or before `asOf`.
  public decimal BookValueAt(DateOnly asOf) => OpeningBookValue - WrittenOff(asOf);

  /// Posted, not reversed months that ended on or before `asOf`.
  public decimal WrittenOff(DateOnly asOf) => _entries.Where(e => e.WrittenOffBy(asOf)).Sum(e => e.DepreciationAmount);

  /// Writes the months still missing, up to `until` (the month holding `until` included). Units of production is
  /// entered by hand.
  public IReadOnlyList<AssetDepreciationEntry> GenerateEntries(DepreciationMethod method, DateOnly until)
  {
    ArgumentNullException.ThrowIfNull(method);

    if (method.Id != MethodId)
      throw new DomainException("The method does not match the schedule.");

    if (method.Kind == DepreciationMethodKind.UnitsOfProduction)
      throw new DomainException("Units-of-production months are entered by hand with the use of the month.");

    if (!IsActive)
      throw new DomainException("This schedule is closed; nothing more is written on it.");

    var generated = new List<AssetDepreciationEntry>();
    var live = LiveEntries.ToList();
    var periodStart = live.Count == 0 ? StartDate : live[^1].PeriodEnd;
    var accumulated = live.Sum(e => e.DepreciationAmount);
    var bookValue = OpeningBookValue - accumulated;
    var end = EndDate!.Value;
    var monthly = DepreciableBase / UsefulLifeMonths;

    while (periodStart <= until && periodStart < end && bookValue > SalvageValue)
    {
      var periodEnd = Min(FirstOfNextMonth(periodStart), end);
      var share = (decimal)(periodEnd.DayNumber - periodStart.DayNumber) / DateTime.DaysInMonth(periodStart.Year, periodStart.Month);
      var left = bookValue - SalvageValue;

      var amount = method.Kind switch
      {
        DepreciationMethodKind.StraightLine => monthly * share,
        // a fixed yearly share of what is left - or the even share of what is left over the remaining life once
        // that gives more, so the life ends on the salvage value without a lump in its last month
        DepreciationMethodKind.DecliningBalance => Math.Max(bookValue * DecliningRate!.Value / 12m, left / RemainingMonths(periodStart, end)) * share,
        _ => throw new DomainException("This depreciation method cannot be worked out automatically.")
      };
      amount = Math.Round(amount, 2, MidpointRounding.AwayFromZero);

      // the last period takes what is left (rounding); no period goes below the salvage value
      if (periodEnd >= end || amount > left)
        amount = left;

      accumulated += amount;
      var entry = AssetDepreciationEntry.Create(Id, periodStart, periodEnd, bookValue, amount, accumulated);
      _entries.Add(entry);
      generated.Add(entry);

      bookValue = entry.BookValueAfter;
      periodStart = periodEnd;
    }

    return generated;
  }

  /// A month entered by hand (units of production).
  public AssetDepreciationEntry AddManualEntry(DateOnly periodStart, DateOnly periodEnd, decimal depreciationAmount)
  {
    if (!IsActive)
      throw new DomainException("This schedule is closed; nothing more is written on it.");

    if (periodStart < StartDate)
      throw new DomainException("The period cannot start before the schedule does.");

    if (_entries.Any(e => !e.IsReversed && periodStart < e.PeriodEnd && periodEnd > e.PeriodStart))
      throw new DomainException("The period overlaps a month already on the schedule.");

    var accumulated = LiveEntries.Sum(e => e.DepreciationAmount);
    var bookValue = OpeningBookValue - accumulated;

    if (bookValue - depreciationAmount < SalvageValue)
      throw new DomainException("This amount would take the asset below its value at the end (salvage).");

    var entry = AssetDepreciationEntry.Create(Id, periodStart, periodEnd, bookValue, depreciationAmount, accumulated + depreciationAmount);
    _entries.Add(entry);
    return entry;
  }

  public void PostEntry(AssetDepreciationEntryId entryId, ActorRef postedBy, DateTime postedAt, DateOnly today) =>
      FindEntry(entryId).Post(postedBy, postedAt, today);

  /// Posts every draft month that ended on or before `upTo` (and by today). Returns how many.
  public int PostEndedEntries(DateOnly upTo, ActorRef postedBy, DateTime postedAt, DateOnly today)
  {
    var limit = upTo < today ? upTo : today;
    var due = _entries.Where(e => !e.Posted && !e.IsReversed && e.LastDay <= limit).OrderBy(e => e.PeriodStart).ToList();
    foreach (var entry in due)
      entry.Post(postedBy, postedAt, today);
    return due.Count;
  }

  public void ReverseEntry(AssetDepreciationEntryId entryId, ActorRef reversedBy, DateTime reversedAt) =>
      FindEntry(entryId).Reverse(reversedBy, reversedAt);

  public void RemoveEntry(AssetDepreciationEntryId entryId)
  {
    var entry = FindEntry(entryId);

    if (entry.Posted)
      throw new DomainException("A posted month cannot be deleted. Reverse it instead - the reversal stays on record.");

    _entries.Remove(entry);
  }

  /// Draft months whose period goes past `lastDay` are taken off (they will never be posted).
  public int RemoveDraftsAfter(DateOnly lastDay)
  {
    var drafts = _entries.Where(e => !e.Posted && e.LastDay > lastDay).ToList();
    foreach (var draft in drafts)
      _entries.Remove(draft);
    return drafts.Count;
  }

  /// Stops the schedule. `endDate` is the first day it no longer covers.
  public void Close(DateOnly endDate)
  {
    if (!IsActive)
      throw new DomainException("This schedule is already closed.");

    if (PostedThrough is { } posted && endDate <= posted)
      throw new DomainException($"Months are posted up to {posted:d MMM yyyy}. The schedule can only close after them - reverse those months first.");

    RemoveDraftsAfter(endDate.AddDays(-1));
    IsActive = false;
    EndDate = endDate;
  }

  /// Opens a schedule again that a disposal closed (when the disposal is cancelled).
  public void Reopen()
  {
    if (IsActive)
      throw new DomainException("This schedule is already running.");

    IsActive = true;
    EndDate = StartDate.AddMonths(UsefulLifeMonths);
  }

  /// A change of estimate: the asset's remaining book value is spread from now on over a new remaining life (and, if
  /// given, a new value at the end). This schedule stops where its posted months stop; the returned one carries on.
  public AssetDepreciationSchedule Revise(
      AssetDepreciationScheduleId newId,
      Asset asset,
      AssetType assetType,
      DepreciationMethod method,
      int remainingLifeMonths,
      decimal salvageValue,
      decimal? decliningRate)
  {
    if (!IsActive)
      throw new DomainException("Only a running schedule can be revised.");

    var from = PostedThrough is { } posted ? posted.AddDays(1) : StartDate;
    var opening = OpeningBookValue - _entries.Where(e => e.Posted && !e.IsReversed).Sum(e => e.DepreciationAmount);

    Close(from);
    return Create(newId, asset, assetType, method, opening, remainingLifeMonths, salvageValue, decliningRate, from);
  }

  private AssetDepreciationEntry FindEntry(AssetDepreciationEntryId entryId) =>
      _entries.FirstOrDefault(e => e.Id == entryId)
      ?? throw new DomainException("That month is not on this schedule.");

  private static DateOnly FirstOfNextMonth(DateOnly date) => new DateOnly(date.Year, date.Month, 1).AddMonths(1);

  private static DateOnly Min(DateOnly a, DateOnly b) => a < b ? a : b;

  /// Months left from `from` to the end of the life, never less than one (the last period's share).
  private static decimal RemainingMonths(DateOnly from, DateOnly end) =>
      Math.Max(1m, (end.DayNumber - from.DayNumber) / AverageDaysPerMonth);
}

/// The one book value of an asset: its cost less every posted, not reversed month that ended by the date, across ALL
/// its schedules (a revision closes one schedule and opens another). The record, the disposal and the reports use it.
public static class AssetBookValue
{
  public static decimal WrittenOff(IEnumerable<AssetDepreciationSchedule> schedules, DateOnly asOf) =>
      schedules.Sum(s => s.WrittenOff(asOf));

  public static decimal NetBookValue(decimal cost, IEnumerable<AssetDepreciationSchedule> schedules, DateOnly asOf) =>
      cost - WrittenOff(schedules, asOf);

  /// The last day of the last posted month on any schedule.
  public static DateOnly? PostedThrough(IEnumerable<AssetDepreciationSchedule> schedules) =>
      schedules.Select(s => s.PostedThrough).Max();
}
