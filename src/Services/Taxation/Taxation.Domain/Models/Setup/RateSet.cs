/// A band as entered: the upper limit (null = "and above", only for the last band) and the value (a factor or an amount).
public sealed record BandInput(decimal? UpTo, decimal Value);

/// The rates of one head for one category (and optionally one town), as entered in Settings. Which parts are used
/// depends on the head's calculation: BANDS (basis, bands, block, round-up, rule above the top band), RATE (basis,
/// rate), FIXED (amount), FORMULA (basis of QTY, optional). OTHER heads (fees) give a rate type and a rate, or bands.
public sealed record HeadRateInput(
  RateBasis? Basis,
  string? UnitLabel,
  IReadOnlyList<BandInput>? Bands,
  decimal? BlockSize,
  bool RoundUp,
  decimal? Rate,
  decimal? Amount,
  RateType? RateType,
  AboveTopRule? AboveTopRule,
  bool ProRata = false);

/// taxation.rate_set: a Board decision with its locality tax factors (ltf_factor) and the rates of every other head
/// (rate_band). A draft is edited freely; approval fixes it and closes the set it replaces the day before. A demand run
/// uses the approved set in force on 1 July of its year, and the annual increase compounds from the set's base year -
/// stated on the set (a new tariff copied from the last one keeps its base year, so the compounding goes on).
public class RateSet : Aggregate<RateSetId>
{
  private readonly List<LtfFactor> _ltfFactors = new();
  private readonly List<RateBand> _rateBands = new();

  public string Name { get; private set; } = default!;
  public string BoardReference { get; private set; } = default!;
  public DateOnly? BoardDecisionDate { get; private set; }
  public DateOnly EffectiveFrom { get; private set; }
  public DateOnly? EffectiveTo { get; private set; }
  public RateSetStatus Status { get; private set; }
  public Guid? ApprovedBy { get; private set; }
  public DateTime? ApprovedAt { get; private set; }

  public IReadOnlyList<LtfFactor> LtfFactors => _ltfFactors.AsReadOnly();
  public IReadOnlyList<RateBand> RateBands => _rateBands.AsReadOnly();

  /// The fiscal year the rates are stated for: the annual increase compounds from here.
  public int BaseFiscalYear { get; private set; }

  /// The first year whose demand uses a set in force from a date: the year of the first 1 July on or after it.
  public static int FirstYearFrom(DateOnly effectiveFrom) =>
      effectiveFrom.Month == 7 && effectiveFrom.Day == 1 ? FiscalYear.Of(effectiveFrom) : FiscalYear.Of(effectiveFrom) + 1;

  private RateSet() { }

  /// baseFiscalYear: the year the rates are stated for; empty = the first year the set is used for.
  public static RateSet Create(RateSetId id, string name, string boardReference, DateOnly? boardDecisionDate, DateOnly effectiveFrom, int? baseFiscalYear = null)
  {
    var set = new RateSet { Id = id, Status = RateSetStatus.Draft };
    set.SetDetails(name, boardReference, boardDecisionDate, effectiveFrom, baseFiscalYear);
    return set;
  }

  public void UpdateDetails(string name, string boardReference, DateOnly? boardDecisionDate, DateOnly effectiveFrom, int? baseFiscalYear = null)
  {
    EnsureDraft();
    SetDetails(name, boardReference, boardDecisionDate, effectiveFrom, baseFiscalYear ?? BaseFiscalYear);
  }

  /// Starts a draft from another set's factors and rates.
  public void CopyRatesFrom(RateSet source)
  {
    EnsureDraft();
    _ltfFactors.Clear();
    _rateBands.Clear();
    foreach (var f in source._ltfFactors)
      _ltfFactors.Add(LtfFactor.Create(LtfFactorId.New(), Id, f.TownId, f.TaxCategory, f.Component, f.BandFrom, f.BandTo, f.Factor));
    foreach (var b in source._rateBands)
      _rateBands.Add(b.CopyTo(RateBandId.New(), Id));
  }

  // ---- locality tax factors ----

  public IReadOnlyList<LtfFactor> LtfBands(Guid townId, TaxCategory category, LtfComponent component) =>
      _ltfFactors.Where(f => f.TownId == townId && f.TaxCategory == category && f.Component == component).OrderBy(f => f.BandFrom).ToList();

  public IReadOnlyList<Guid> TownsWithFactors => _ltfFactors.Select(f => f.TownId).Distinct().ToList();

  /// Replaces a town's factors for one category and component: bands of rising limits, only the last open.
  public void SetLtfFactors(Guid townId, TaxCategory category, LtfComponent component, IReadOnlyList<BandInput> bands)
  {
    EnsureDraft();
    if (townId == Guid.Empty)
      throw new DomainException("Choose the town.");
    if (category == TaxCategory.Exempt)
      throw new DomainException("Exempt properties are not assessed; give factors for residential or commercial.");

    var what = $"{(category == TaxCategory.Com ? "Commercial" : "Residential")} {(component == LtfComponent.Land ? "land" : "covered area")} factors";
    var checkedBands = CheckBands(bands, what);

    _ltfFactors.RemoveAll(f => f.TownId == townId && f.TaxCategory == category && f.Component == component);
    var from = 0m;
    foreach (var band in checkedBands)
    {
      _ltfFactors.Add(LtfFactor.Create(LtfFactorId.New(), Id, townId, category, component, from, band.UpTo, Guard.NotNegative(band.Value, what)));
      from = band.UpTo ?? from;
    }
  }

  public void RemoveTownFactors(Guid townId)
  {
    EnsureDraft();
    _ltfFactors.RemoveAll(f => f.TownId == townId);
  }

  // ---- rates of the other heads ----

  /// The rows of a head for a category in a town: a town's own rows win over the all-towns rows, and a category's own
  /// rows win over the both-categories rows.
  public IReadOnlyList<RateBand> HeadRows(TaxHeadId headId, TaxCategory? category, Guid? townId)
  {
    var rows = _rateBands.Where(b => b.HeadId == headId).ToList();
    IEnumerable<RateBand> Pick(Guid? town, TaxCategory? cat) => rows.Where(b => b.TownId == town && b.TaxCategory == cat);

    foreach (var town in townId is null ? new Guid?[] { null } : new[] { townId, null })
    foreach (var cat in category is null ? new TaxCategory?[] { null } : new[] { category, null })
    {
      var found = Pick(town, cat).OrderBy(b => b.BandFrom ?? 0m).ToList();
      if (found.Count > 0)
        return found;
    }
    return [];
  }

  /// Replaces the rates of a head for one category (null = both) and town (null = all towns).
  public void SetHeadRates(TaxHead head, TaxCategory? category, Guid? townId, HeadRateInput input)
  {
    EnsureDraft();
    if (category == TaxCategory.Exempt)
      throw new DomainException("Exempt properties are not charged.");

    var who = category switch { TaxCategory.Com => "commercial", TaxCategory.Res => "residential", _ => "all" };
    var rows = head.HeadKind == HeadKind.Regular ? RegularRows(head, category, input, who) : FeeRows(head, input, who);

    _rateBands.RemoveAll(b => b.HeadId == head.Id && b.TaxCategory == category && b.TownId == townId);
    foreach (var row in rows)
      _rateBands.Add(RateBand.Create(RateBandId.New(), Id, head.Id, townId, category, row.Basis, row.UnitLabel, row.RoundUp, row.BlockSize,
        row.BandFrom, row.BandTo, row.Rate, row.RateType, input.ProRata, row.AboveTopRule));
  }

  public void RemoveHeadRates(TaxHeadId headId, TaxCategory? category, Guid? townId)
  {
    EnsureDraft();
    _rateBands.RemoveAll(b => b.HeadId == headId && b.TaxCategory == category && b.TownId == townId);
  }

  private sealed record RowSpec(RateBasis Basis, string? UnitLabel, bool RoundUp, decimal? BlockSize, decimal? BandFrom, decimal? BandTo, decimal Rate, RateType RateType, AboveTopRule? AboveTopRule);

  private static List<RowSpec> RegularRows(TaxHead head, TaxCategory? category, HeadRateInput input, string who)
  {
    if (category is null)
      throw new DomainException($"Give the rates of {head.Name} for residential or commercial properties.");
    if (!head.AppliesToCategory(category.Value))
      throw new DomainException($"{head.Name} does not apply to {who} properties.");

    switch (head.CalcMethod)
    {
      case CalcMethod.Bands:
      {
        var basis = QuantityBasis(input.Basis, who);
        var unit = UnitLabel(basis, input.UnitLabel, who);
        var bands = CheckBands(input.Bands, $"The {who} bands of {head.Name}");
        var block = input.BlockSize is { } b ? Guard.Positive(b, "Block size") : 1m;
        var rule = input.AboveTopRule ?? AboveTopRule.TopRateStays;
        var rows = new List<RowSpec>();
        var from = 0m;
        for (var i = 0; i < bands.Count; i++)
        {
          var top = i == bands.Count - 1;
          rows.Add(new(basis, unit, input.RoundUp, block, from, bands[i].UpTo, Guard.NotNegative(bands[i].Value, $"{who} band {i + 1}"), RateType.Flat, top ? rule : null));
          from = bands[i].UpTo ?? from;
        }
        return rows;
      }
      case CalcMethod.Rate:
      {
        var basis = QuantityBasis(input.Basis, who);
        var rate = input.Rate ?? throw new DomainException($"Give the {who} rate of {head.Name}.");
        return [new(basis, UnitLabel(basis, input.UnitLabel, who), input.RoundUp, null, null, null, Guard.NotNegative(rate, "Rate"), RateType.PerUnit, null)];
      }
      case CalcMethod.Fixed:
      {
        var amount = input.Amount ?? input.Rate ?? throw new DomainException($"Give the {who} amount of {head.Name}.");
        return [new(RateBasis.Fixed, null, false, null, null, null, Guard.NotNegative(amount, "Amount"), RateType.Flat, null)];
      }
      case CalcMethod.Formula:
      {
        // the formula is on the head; the row only names what QTY counts
        var basis = input.Basis is null ? RateBasis.Fixed : QuantityBasis(input.Basis, who);
        return [new(basis, basis == RateBasis.Fixed ? null : UnitLabel(basis, input.UnitLabel, who), false, null, null, null, 0m, RateType.Flat, null)];
      }
      default:
        throw new DomainException($"{head.Name} is built in; its rates are not entered here.");
    }
  }

  private static List<RowSpec> FeeRows(TaxHead head, HeadRateInput input, string who)
  {
    var basis = input.Basis ?? RateBasis.Fixed;
    var type = input.RateType ?? (basis == RateBasis.Fixed ? RateType.Flat : basis == RateBasis.PercentOfValue ? RateType.Percent : RateType.PerUnit);
    if (basis == RateBasis.PercentOfValue && type != RateType.Percent)
      throw new DomainException("A rate on the value is a percentage.");
    if (type == RateType.Percent && basis != RateBasis.PercentOfValue)
      throw new DomainException("A percentage is charged on the value (basis PERCENT_OF_VALUE).");

    if (input.Bands is { Count: > 0 })
    {
      var bands = CheckBands(input.Bands, $"The bands of {head.Name}");
      var rows = new List<RowSpec>();
      var from = 0m;
      foreach (var band in bands)
      {
        rows.Add(new(basis, UnitLabel(basis, input.UnitLabel, who), input.RoundUp, null, from, band.UpTo, Guard.NotNegative(band.Value, "Rate"), type, null));
        from = band.UpTo ?? from;
      }
      return rows;
    }

    var rate = input.Rate ?? input.Amount ?? throw new DomainException($"Give the rate of {head.Name}.");
    return [new(basis, basis == RateBasis.Custom ? UnitLabel(basis, input.UnitLabel, who) : Guard.Text(input.UnitLabel, 60, "Unit"), input.RoundUp, null, null, null, Guard.NotNegative(rate, "Rate"), type, null)];
  }

  private static RateBasis QuantityBasis(RateBasis? basis, string who) => basis switch
  {
    null => throw new DomainException($"Choose what the {who} rate is charged on (rooms, kanal, marla, sq ft, sq yd or a quantity)."),
    RateBasis.Rooms or RateBasis.Kanal or RateBasis.Marla or RateBasis.Sqft or RateBasis.Sqyd or RateBasis.Custom => basis.Value,
    _ => throw new DomainException($"A regular head is charged on rooms, kanal, marla, sq ft, sq yd or a quantity entered per property, not {EnumText.Words(basis.Value)}.")
  };

  private static string? UnitLabel(RateBasis basis, string? unit, string who)
  {
    if (basis != RateBasis.Custom)
      return null;
    return Guard.Text(unit, 60, "Unit") ?? throw new DomainException($"Name the {who} quantity (e.g. sq ft of signboard).");
  }

  /// Rising limits, only the last band open ("and above"), at least one band.
  private static List<BandInput> CheckBands(IReadOnlyList<BandInput>? bands, string what)
  {
    if (bands is null || bands.Count == 0)
      throw new DomainException($"{what}: give at least one band.");
    if (bands.Count > 50)
      throw new DomainException($"{what}: at most 50 bands.");

    decimal? previous = null;
    for (var i = 0; i < bands.Count; i++)
    {
      var upTo = bands[i].UpTo;
      if (upTo is null && i < bands.Count - 1)
        throw new DomainException($"{what}: only the last band may be open (\"and above\").");
      if (upTo is not null && (upTo <= 0 || (previous is not null && upTo <= previous)))
        throw new DomainException($"{what}: the band limits must rise.");
      previous = upTo;
    }
    return bands.ToList();
  }

  // ---- approval ----

  /// Approves the set from its effective date. Every active regular head the module computes must have its rates for
  /// the categories it applies to, and every town assigned to a circle must have its locality factors.
  public void Approve(Guid actor, DateTime now, IReadOnlyList<TaxHead> regularHeads, IReadOnlyList<(Guid TownId, string TownCode)> assignedTowns, IReadOnlyList<RateSet> approvedSets)
  {
    EnsureDraft();

    var problems = new List<string>();
    foreach (var head in regularHeads.Where(h => h.IsActive && h.HeadKind == HeadKind.Regular && !h.IsBuiltin))
    foreach (var category in new[] { TaxCategory.Res, TaxCategory.Com }.Where(head.AppliesToCategory))
    {
      var who = category == TaxCategory.Com ? "commercial" : "residential";
      bool Has(Guid? town) => _rateBands.Any(b => b.HeadId == head.Id && b.TownId == town && (b.TaxCategory == category || b.TaxCategory == null));
      // rates for all towns, or a town's own rates for every town in a circle
      if (Has(null))
        continue;
      var missing = assignedTowns.Where(t => !Has(t.TownId)).Select(t => t.TownCode).ToList();
      if (missing.Count == assignedTowns.Count)
        problems.Add($"{head.Name}: no {who} rates");
      else if (missing.Count > 0)
        problems.Add($"{head.Name}: no {who} rates for {string.Join(", ", missing)} (give them, or rates for all towns)");
    }
    foreach (var town in assignedTowns)
    foreach (var category in new[] { TaxCategory.Res, TaxCategory.Com })
    foreach (var component in new[] { LtfComponent.Land, LtfComponent.Covered })
    {
      if (LtfBands(town.TownId, category, component).Count == 0)
      {
        problems.Add($"{town.TownCode}: no {(category == TaxCategory.Com ? "commercial" : "residential")} {(component == LtfComponent.Land ? "land" : "covered area")} factors");
      }
    }
    if (problems.Count > 0)
      throw new DomainException("The rate set is not complete: " + string.Join("; ", problems.Distinct().Take(20)) + ".");

    var clash = approvedSets.FirstOrDefault(s => s.Id != Id && s.EffectiveFrom >= EffectiveFrom);
    if (clash is not null)
      throw new DomainException($"'{clash.Name}' is already approved from {clash.EffectiveFrom:yyyy-MM-dd}. This set must start after it.");

    foreach (var previous in approvedSets.Where(s => s.Id != Id && s.Status == RateSetStatus.Approved && (s.EffectiveTo is null || s.EffectiveTo >= EffectiveFrom)))
      previous.SupersedeFrom(EffectiveFrom);

    Status = RateSetStatus.Approved;
    ApprovedBy = actor;
    ApprovedAt = now;
  }

  public bool Covers(DateOnly date) => Status != RateSetStatus.Draft && EffectiveFrom <= date && (EffectiveTo is null || EffectiveTo >= date);

  private void SupersedeFrom(DateOnly nextStart)
  {
    EffectiveTo = nextStart.AddDays(-1);
    Status = RateSetStatus.Superseded;
  }

  private void EnsureDraft()
  {
    if (Status != RateSetStatus.Draft)
      throw new DomainException($"'{Name}' is approved and can no longer change. Start a new rate set from it.");
  }

  private void SetDetails(string name, string boardReference, DateOnly? boardDecisionDate, DateOnly effectiveFrom, int? baseFiscalYear)
  {
    Name = Guard.RequiredText(name, 150, "Rate set name");
    BoardReference = Guard.RequiredText(boardReference, 100, "Board reference");
    BoardDecisionDate = boardDecisionDate;
    if (boardDecisionDate is { } decided && decided > effectiveFrom.AddYears(1))
      throw new DomainException("The Board decision date is more than a year after the rates take effect.");
    EffectiveFrom = effectiveFrom;
    var first = FirstYearFrom(effectiveFrom);
    var baseYear = FiscalYear.Valid(baseFiscalYear ?? first, "The increase base year");
    if (baseYear > first)
      throw new DomainException($"The rates are first used for FY {FiscalYear.Label(first)}; the annual increase cannot be counted from a later year.");
    BaseFiscalYear = baseYear;
  }
}

/// taxation.ltf_factor: one band of a town's locality tax factor table.
public class LtfFactor : Entity<LtfFactorId>
{
  public RateSetId RateSetId { get; private set; } = default!;
  public Guid TownId { get; private set; }
  public TaxCategory TaxCategory { get; private set; }
  public LtfComponent Component { get; private set; }
  public decimal BandFrom { get; private set; }
  public decimal? BandTo { get; private set; }
  public decimal Factor { get; private set; }

  private LtfFactor() { }

  internal static LtfFactor Create(LtfFactorId id, RateSetId rateSetId, Guid townId, TaxCategory category, LtfComponent component, decimal from, decimal? to, decimal factor) => new()
  {
    Id = id,
    RateSetId = rateSetId,
    TownId = townId,
    TaxCategory = category,
    Component = component,
    BandFrom = from,
    BandTo = to,
    Factor = factor
  };
}

/// taxation.rate_band: one rate of a head (a band of a table, a rate per unit or a fixed amount).
public class RateBand : Entity<RateBandId>
{
  public RateSetId RateSetId { get; private set; } = default!;
  public TaxHeadId HeadId { get; private set; } = default!;
  public Guid? TownId { get; private set; }
  public TaxCategory? TaxCategory { get; private set; }
  public RateBasis Basis { get; private set; }
  public string? UnitLabel { get; private set; }
  public bool RoundUp { get; private set; }
  public decimal? BlockSize { get; private set; }
  public decimal? BandFrom { get; private set; }
  public decimal? BandTo { get; private set; }
  public decimal Rate { get; private set; }
  public RateType RateType { get; private set; }
  public bool ProRata { get; private set; }
  public AboveTopRule? AboveTopRule { get; private set; }

  private RateBand() { }

  internal static RateBand Create(RateBandId id, RateSetId rateSetId, TaxHeadId headId, Guid? townId, TaxCategory? category, RateBasis basis, string? unitLabel,
      bool roundUp, decimal? blockSize, decimal? from, decimal? to, decimal rate, RateType rateType, bool proRata, AboveTopRule? aboveTopRule) => new()
  {
    Id = id,
    RateSetId = rateSetId,
    HeadId = headId,
    TownId = townId,
    TaxCategory = category,
    Basis = basis,
    UnitLabel = unitLabel,
    RoundUp = roundUp,
    BlockSize = blockSize,
    BandFrom = from,
    BandTo = to,
    Rate = rate,
    RateType = rateType,
    ProRata = proRata,
    AboveTopRule = aboveTopRule
  };

  internal RateBand CopyTo(RateBandId id, RateSetId rateSetId) =>
      Create(id, rateSetId, HeadId, TownId, TaxCategory, Basis, UnitLabel, RoundUp, BlockSize, BandFrom, BandTo, Rate, RateType, ProRata, AboveTopRule);
}
