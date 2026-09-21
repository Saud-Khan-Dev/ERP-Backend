/// Only one live (is_active) schedule per asset — partial unique index in the DB.
public class AssetDepreciationSchedule : Aggregate<AssetDepreciationScheduleId>
{
  private readonly List<AssetDepreciationEntry> _entries = new();

  public AssetId AssetId { get; private set; } = default!;
  public DepreciationMethodId MethodId { get; private set; } = default!;
  public int UsefulLifeMonths { get; private set; }
  public decimal SalvageValue { get; private set; }
  /// Cost minus salvage, snapshotted at start.
  public decimal DepreciableBase { get; private set; }
  /// Required for DECLINING_BALANCE (annual rate, e.g. 0.20).
  public decimal? DecliningRate { get; private set; }
  public DateOnly StartDate { get; private set; }
  public DateOnly? EndDate { get; private set; }
  public bool IsActive { get; private set; }
  public IReadOnlyList<AssetDepreciationEntry> Entries => _entries.AsReadOnly();

  public static AssetDepreciationSchedule Create(
      AssetDepreciationScheduleId id,
      Asset asset,
      AssetType assetType,
      DepreciationMethod method,
      decimal acquisitionCost,
      int usefulLifeMonths,
      decimal salvageValue,
      decimal? decliningRate,
      DateOnly startDate)
  {
    ArgumentNullException.ThrowIfNull(asset);
    ArgumentNullException.ThrowIfNull(assetType);
    ArgumentNullException.ThrowIfNull(method);

    if (!assetType.IsDepreciable)
      throw new DomainException($"Assets of type '{assetType.Name.Value}' are not depreciable.");

    if (!method.IsActive)
      throw new DomainException($"Depreciation method '{method.Name.Value}' is inactive.");

    if (usefulLifeMonths <= 0)
      throw new DomainException("useful_life_months must be greater than zero.");

    if (salvageValue < 0)
      throw new DomainException("Salvage value cannot be negative.");

    if (salvageValue > acquisitionCost)
      throw new DomainException("Salvage value cannot exceed the acquisition cost.");

    if (method.Code.Value == DepreciationMethod.DecliningBalance && decliningRate is null or <= 0 or > 1)
      throw new DomainException("DECLINING_BALANCE requires a declining_rate between 0 and 1.");

    return new AssetDepreciationSchedule
    {
      Id = id,
      AssetId = asset.Id,
      MethodId = method.Id,
      UsefulLifeMonths = usefulLifeMonths,
      SalvageValue = salvageValue,
      DepreciableBase = acquisitionCost - salvageValue,
      DecliningRate = method.Code.Value == DepreciationMethod.DecliningBalance ? decliningRate : null,
      StartDate = startDate,
      EndDate = startDate.AddMonths(usefulLifeMonths),
      IsActive = true
    };
  }

  public void Deactivate(DateOnly? endDate = null)
  {
    IsActive = false;
    if (endDate.HasValue)
      EndDate = endDate;
  }

  /// Generates the monthly entries that are still missing, up to `until` (inclusive of the period containing it).
  /// STRAIGHT_LINE: base / useful_life per month. DECLINING_BALANCE: opening book value x rate / 12, floored at salvage.
  /// UNITS_OF_PRODUCTION entries are recorded manually via AddManualEntry.
  public IReadOnlyList<AssetDepreciationEntry> GenerateEntries(DepreciationMethod method, DateOnly until, decimal acquisitionCost)
  {
    ArgumentNullException.ThrowIfNull(method);

    if (method.Id != MethodId)
      throw new DomainException("The supplied method does not match the schedule.");

    if (method.Code.Value == DepreciationMethod.UnitsOfProduction)
      throw new DomainException("UNITS_OF_PRODUCTION entries must be recorded manually with the units consumed.");

    if (!IsActive)
      throw new DomainException("Cannot generate entries for an inactive schedule.");

    var generated = new List<AssetDepreciationEntry>();
    var live = _entries.Where(e => !e.IsReversed).OrderBy(e => e.PeriodStart).ToList();
    var periodStart = live.Count == 0 ? StartDate : live[^1].PeriodEnd;
    var accumulated = live.Sum(e => e.DepreciationAmount);
    var bookValue = acquisitionCost - accumulated;
    var monthlyStraightLine = Math.Round(DepreciableBase / UsefulLifeMonths, 2, MidpointRounding.AwayFromZero);

    while (periodStart <= until && periodStart < EndDate!.Value && bookValue > SalvageValue)
    {
      var periodEnd = periodStart.AddMonths(1);
      var isLastPeriod = periodEnd >= EndDate.Value;

      var amount = method.Code.Value switch
      {
        DepreciationMethod.StraightLine => monthlyStraightLine,
        DepreciationMethod.DecliningBalance => Math.Round(bookValue * DecliningRate!.Value / 12m, 2, MidpointRounding.AwayFromZero),
        _ => throw new DomainException($"Unsupported depreciation method '{method.Code.Value}'.")
      };

      // never depreciate below salvage; the final period absorbs rounding differences
      var maxAmount = bookValue - SalvageValue;
      if (isLastPeriod || amount > maxAmount)
        amount = maxAmount;

      accumulated += amount;
      var entry = AssetDepreciationEntry.Create(Id, periodStart, periodEnd, bookValue, amount, accumulated);
      _entries.Add(entry);
      generated.Add(entry);

      bookValue = entry.BookValueAfter;
      periodStart = periodEnd;
    }

    return generated;
  }

  public AssetDepreciationEntry AddManualEntry(DateOnly periodStart, DateOnly periodEnd, decimal depreciationAmount, decimal acquisitionCost)
  {
    if (!IsActive)
      throw new DomainException("Cannot add entries to an inactive schedule.");

    if (_entries.Any(e => !e.IsReversed && periodStart < e.PeriodEnd && periodEnd > e.PeriodStart))
      throw new DomainException("The period overlaps an existing entry.");

    var live = _entries.Where(e => !e.IsReversed).ToList();
    var accumulated = live.Sum(e => e.DepreciationAmount);
    var bookValue = acquisitionCost - accumulated;

    if (bookValue - depreciationAmount < SalvageValue)
      throw new DomainException("This entry would depreciate the asset below its salvage value.");

    var entry = AssetDepreciationEntry.Create(Id, periodStart, periodEnd, bookValue, depreciationAmount, accumulated + depreciationAmount);
    _entries.Add(entry);
    return entry;
  }

  public void PostEntry(AssetDepreciationEntryId entryId, Guid? postedBy, DateTime postedAt) =>
      FindEntry(entryId).Post(postedBy, postedAt);

  public void ReverseEntry(AssetDepreciationEntryId entryId, Guid? reversedBy, DateTime reversedAt) =>
      FindEntry(entryId).Reverse(reversedBy, reversedAt);

  public void RemoveEntry(AssetDepreciationEntryId entryId)
  {
    var entry = FindEntry(entryId);

    if (entry.Posted)
      throw new DomainException("Posted entries cannot be deleted. Reverse them instead.");

    _entries.Remove(entry);
  }

  public decimal NetBookValue(decimal acquisitionCost) =>
      acquisitionCost - _entries.Where(e => e.Posted && !e.IsReversed).Sum(e => e.DepreciationAmount);

  private AssetDepreciationEntry FindEntry(AssetDepreciationEntryId entryId) =>
      _entries.FirstOrDefault(e => e.Id == entryId)
      ?? throw new DomainException($"Depreciation entry {entryId.Value} does not belong to this schedule.");
}
