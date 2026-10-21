/// One head of a computed demand. Amount = BaseAmount + UpliftAmount + IncreaseAmount + OverrideAmount.
///   NORMAL   the formula (BaseAmount), the completion uplift on it, the annual increase on both
///   POLICY   a special policy's amount (BaseAmount, its own increase included)
///   SPECIAL  a fixed special charge (BaseAmount) and, when it follows the increase, the increase on it
///   MINIMUM  the normal computation topped up to the floor (OverrideAmount)
/// NormalAmount is what the normal formula gives, kept so the head can be switched back.
public sealed record ComputedLine(
  TaxHeadId HeadId,
  string HeadCode,
  string HeadName,
  decimal? BasisQty,
  string? BasisUnit,
  LtfFactorId? LtfFactorId,
  RateBandId? RateBandId,
  decimal BaseAmount,
  decimal UpliftAmount,
  decimal IncreaseAmount,
  decimal OverrideAmount,
  decimal Amount,
  LineMode Mode,
  TaxFileOverrideId? OverrideId,
  SpecialPolicyId? PolicyId,
  decimal NormalAmount,
  decimal NormalBase,
  decimal NormalUplift,
  decimal NormalIncrease,
  string Explanation,
  string NormalExplanation);

/// The year's computation for one tax file. Suspended when a litigation hold is open on 1 July or the file is not
/// assessable; Notes say what was decided along the way (uplift, heads not charged, the increase).
public sealed record Assessment(
  bool Suspended,
  string? SuspendedReason,
  IReadOnlyList<ComputedLine> Lines,
  IReadOnlyList<string> Notes,
  TaxCategory CategoryUsed,
  decimal UpliftPct,
  int IncreaseYears)
{
  public decimal Total => Lines.Sum(l => l.Amount);
}

public sealed record AssessmentInput(
  TaxFile File,
  TaxFileBasis Basis,
  int FiscalYear,
  IReadOnlyList<TaxHead> Heads,
  RateSet RateSet,
  TaxSettings Settings,
  IReadOnlyList<SpecialPolicy> Policies);

/// The rules of the annual demand, one for one with the Section's sheets and the recordings of 12 Oct 2026. Pure: it
/// reads the file, its basis, the rate set in force and the settings, and returns the lines; nothing is saved.
public static class TaxCalculator
{
  public static Assessment Assess(AssessmentInput input)
  {
    var (file, basis, fy, heads, rateSet, settings, policies) = input;
    var yearStart = FiscalYear.Start(fy);
    var notes = new List<string>();
    var lines = new List<ComputedLine>();

    if (!file.IsAssessable)
      return new Assessment(true, $"The file is {EnumText.Words(file.TaxStatus)}{(file.TaxCategory == TaxCategory.Exempt ? " (exempt category)" : "")}; it is not assessed.", lines, notes, file.TaxCategory, 100m, 0);

    if (file.HoldOn(yearStart) is { } hold)
      return new Assessment(true, $"Litigation hold since {hold.StartedOn:yyyy-MM-dd} ({hold.Reason}): no tax or fee is levied while it is open.", lines, notes, file.TaxCategory, 100m, 0);

    // completion-certificate uplift: the grace years after the plan; held periods do not count (Part 1 11:31)
    var category = file.TaxCategory;
    var upliftPct = 100m;
    var completed = basis.CompletionDate is { } cc && cc <= yearStart;
    if (basis.BpaDate is { } bpa && !completed && bpa < yearStart)
    {
      var held = file.HoldDaysBetween(bpa, yearStart);
      var rawYears = (yearStart.DayNumber - bpa.DayNumber) / 365.25m;
      var years = rawYears - held / 365.25m;
      if (years >= settings.CcGraceYears)
      {
        var rateBasis = file.UpliftRateBasis ?? settings.CcUpliftRateBasis;
        category = rateBasis == UpliftRateBasis.Commercial ? TaxCategory.Com : file.TaxCategory;
        upliftPct = file.UpliftPct ?? (file.TaxCategory == TaxCategory.Com ? settings.CcUpliftPctCom : settings.CcUpliftPctRes);
        var own = file.UpliftPct is not null || file.UpliftRateBasis is not null ? " (property setting)" : "";
        notes.Add($"No completion certificate {settings.CcGraceYears} years after the building plan ({bpa:yyyy-MM-dd}{(held > 0 ? $", {held} held days not counted" : "")}): "
          + $"{(category == TaxCategory.Com ? "commercial" : "residential")} rate at {Rupees.Number(upliftPct)} %{own}.");
      }
      else if (held > 0 && rawYears >= settings.CcGraceYears)
      {
        notes.Add($"Completion clock: {held} days under litigation hold are not counted, so the {settings.CcGraceYears}-year grace after the building plan ({bpa:yyyy-MM-dd}) has not run out yet; no uplift.");
      }
    }

    var increaseYears = Math.Max(0, fy - rateSet.BaseFiscalYear);
    var increasePct = settings.AnnualIncreasePct;
    var quantities = Quantities.Of(file, basis);
    // the locality factors are variables of every formula head too (0 when the town has none)
    var ltfVars = quantities.Variables(
      Pick(rateSet.LtfBands(file.TownId, category, LtfComponent.Land), quantities.LandSqyd)?.Factor ?? 0m,
      Pick(rateSet.LtfBands(file.TownId, category, LtfComponent.Covered), quantities.CoveredSqft)?.Factor ?? 0m);

    var regular = heads.Where(h => h.HeadKind == HeadKind.Regular && h.IsActive && h.AppliesToCategory(file.TaxCategory))
      .OrderBy(h => h.SortOrder).ThenBy(h => h.Name, StringComparer.OrdinalIgnoreCase).ToList();

    foreach (var head in regular)
    {
      switch (head.CalcMethod)
      {
        case CalcMethod.Ltf:
        {
          if (basis.TaxableLandSqyd is null)
            throw new DomainException($"{file.PropertyCode} has no land area in Property. Record its measurement there and sync the file.");
          var land = Pick(rateSet.LtfBands(file.TownId, category, LtfComponent.Land), quantities.LandSqyd);
          var covered = Pick(rateSet.LtfBands(file.TownId, category, LtfComponent.Covered), quantities.CoveredSqft);
          if (land is null || covered is null)
            throw new DomainException($"'{rateSet.Name}' has no {(category == TaxCategory.Com ? "commercial" : "residential")} locality factors for the town of {file.PropertyCode}.");

          var vars = quantities.Variables(land.Factor, covered.Factor);
          var formula = TaxFormula.Parse(head.FormulaFor(category) ?? head.FormulaCom ?? "(A * LTF_LAND) + (B * LTF_COVERED)");
          var raw = formula.Evaluate(vars);
          var baseAmount = Rupees.Round(raw);
          var withUplift = Rupees.Round(raw * upliftPct / 100m);
          var text = $"A={Rupees.Number(quantities.LandSqyd)} sq yd × {Rupees.Number(land.Factor)} + B={Rupees.Number(quantities.CoveredSqft)} sq ft × {Rupees.Number(covered.Factor)}"
            + (upliftPct != 100m ? $" × {Rupees.Number(upliftPct)} %" : "") + (category != file.TaxCategory ? " (commercial factors)" : "");
          lines.Add(Line(head, file, fy, rateSet, settings, policies, notes, increaseYears, baseAmount, withUplift - baseAmount, text, quantities.LandSqyd, "SQYD", land.Id, null));
          break;
        }
        case CalcMethod.Agreement:
        {
          var rents = file.RentalsFor(fy).Select(a => a.AmountFor(fy)).Where(r => r is not null).Select(r => r!.Value).ToList();
          if (rents.Count == 0)
            continue;
          lines.Add(Line(head, file, fy, rateSet, settings, policies, notes, increaseYears, rents.Sum(r => r.Amount), 0m, string.Join("; ", rents.Select(r => r.Explanation)), null, null, null, null));
          break;
        }
        default:
        {
          var share = 1m;
          var fromNote = "";
          if (head.EffectiveFrom is { } headFrom && headFrom > FiscalYear.End(fy))
          {
            notes.Add($"{head.Name}: charged from {headFrom:dd MMM yyyy}, after this year.");
            continue;
          }
          if (head.ChargedFrom == ChargedFrom.Registration)
          {
            share = FiscalYear.ShareFrom(fy, file.RegisteredOn);
            if (share <= 0)
            {
              notes.Add($"{head.Name}: registered {file.RegisteredOn:yyyy-MM-dd}, after this year.");
              continue;
            }
            if (share < 1)
              fromNote = $"; from registration {file.RegisteredOn:yyyy-MM-dd} ({Math.Round(share * 100)} % of the year)";
          }
          else if (head.ChargedFrom == ChargedFrom.Facility)
          {
            var from = file.HeadValue(head.Id)?.FacilityFrom ?? (head.Code == HeadCodes.Water ? file.WaterFacilityFrom : null);
            var facility = head.FacilityLabel ?? "facility";
            if (from is null)
            {
              notes.Add($"{head.Name}: no {facility} taken, so no {head.Name.ToLowerInvariant()}.");
              continue;
            }
            share = FiscalYear.ShareFrom(fy, from);
            if (share <= 0)
            {
              notes.Add($"{head.Name}: {facility} from {from:yyyy-MM-dd}, after this year.");
              continue;
            }
            if (share < 1)
              fromNote = $"; {facility} from {from:yyyy-MM-dd} ({Math.Round(share * 100)} % of the year)";
          }

          // a head added after the rate set was approved is charged once a rate set gives it rates
          var rows = rateSet.HeadRows(head.Id, file.TaxCategory, file.TownId);
          if (rows.Count == 0)
          {
            notes.Add($"{head.Name}: '{rateSet.Name}' has no {(file.TaxCategory == TaxCategory.Com ? "commercial" : "residential")} rates for it, so it is not charged.");
            continue;
          }
          var result = HeadAmount(head, rows, quantities, file.HeadValue(head.Id)?.Quantity, ltfVars);
          if (result is null || result.Value.Amount * share <= 0)
            continue;
          var r = result.Value;
          lines.Add(Line(head, file, fy, rateSet, settings, policies, notes, increaseYears, Rupees.Round(r.Amount * share), 0m, r.Explanation + fromNote, r.Quantity, r.Unit, null, r.Row.Id));
          break;
        }
      }
    }

    if (increaseYears > 0)
    {
      var increased = regular.Where(h => h.AppliesAnnualIncrease).Select(h => h.Name.ToLowerInvariant()).ToList();
      if (increased.Count > 0)
        notes.Add($"Annual increase {Rupees.Number(increasePct)} % compounded for {increaseYears} year(s) on {string.Join(", ", increased)}; annual rent follows its agreement.");
    }

    return new Assessment(false, null, lines, notes, category, upliftPct, increaseYears);
  }

  /// The overrides, special policies and annual increase applied to a head's computed amount.
  private static ComputedLine Line(TaxHead head, TaxFile file, int fy, RateSet rateSet, TaxSettings settings, IReadOnlyList<SpecialPolicy> policies, List<string> notes,
      int years, decimal baseAmount, decimal uplift, string explanation, decimal? qty, string? unit, LtfFactorId? factorId, RateBandId? bandId)
  {
    var increasePct = settings.AnnualIncreasePct;
    var core = baseAmount + uplift;
    var normalIncrease = head.AppliesAnnualIncrease && years > 0 ? Rupees.Round(core * (Compound(increasePct, years) - 1m)) : 0m;
    var normal = core + normalIncrease;

    var ov = head.OverrideAllowed ? file.OverrideFor(head.Id, FiscalYear.Start(fy)) : null;
    var mode = LineMode.Normal;
    decimal lineBase = baseAmount, lineUplift = uplift, increase = normalIncrease, top = 0m;
    var text = explanation;
    SpecialPolicyId? policyId = null;

    if (ov is { Mode: OverrideMode.Special, PolicyId: not null })
    {
      var policy = policies.FirstOrDefault(p => p.Id == ov.PolicyId);
      if (policy is not null && policy.AvailableFor(head.Id, file.TaxCategory, fy))
      {
        var pa = PolicyAmount(policy, head, file, fy, normal, rateSet, settings);
        (lineBase, lineUplift, increase, mode, policyId, text) = (pa.Amount, 0m, 0m, LineMode.Policy, policy.Id, pa.Explanation);
      }
      else
      {
        notes.Add($"{head.Name}: the special policy assigned to this property is not in force for FY {FiscalYear.Label(fy)}; the normal tax is charged.");
      }
    }
    else if (ov is { Mode: OverrideMode.Special, Amount: { } special })
    {
      var incYears = ov.FollowsIncrease ? Math.Max(0, fy - Math.Max(rateSet.BaseFiscalYear, FiscalYear.Of(ov.EffectiveFrom))) : 0;
      lineBase = Rupees.Round(special);
      lineUplift = 0m;
      increase = head.AppliesAnnualIncrease && incYears > 0 ? Rupees.Round(lineBase * (Compound(increasePct, incYears) - 1m)) : 0m;
      mode = LineMode.Special;
      text = $"special charge: {ov.Reason} (formula would give {Rupees.Text(core)})";
    }

    var amount = lineBase + lineUplift + increase;
    if (ov is { Mode: OverrideMode.Minimum, Amount: { } floor } && amount < floor)
    {
      top = Rupees.Round(floor - amount);
      notes.Add($"{head.Name}: computed {Rupees.Text(amount)} is below the minimum {Rupees.Text(floor)} ({ov.Reason}); topped up by {Rupees.Text(top)}.");
      amount += top;
      mode = LineMode.Minimum;
    }

    return new ComputedLine(head.Id, head.Code, head.Name, qty, unit, factorId, bandId, lineBase, lineUplift, increase, top, amount, mode,
      mode == LineMode.Normal ? null : ov?.Id, policyId, normal, baseAmount, uplift, normalIncrease, text, explanation);
  }

  /// A special policy's amount for the year. The normal amount is the head's normal tax with its increase.
  public static (decimal Amount, string Explanation) PolicyAmount(SpecialPolicy policy, TaxHead head, TaxFile file, int fy, decimal normal, RateSet rateSet, TaxSettings settings)
  {
    var basis = file.CurrentBasis;
    var v = policy.Value;
    var rooms = file.Rooms ?? 0;
    var sqft = basis?.CoveredAreaSqft ?? 0m;
    var kanal = basis?.TaxableKanal ?? 0m;
    var (amount, text) = policy.Method switch
    {
      PolicyMethod.PerMonth => (v * 12, $"{Rupees.Text(v)} a month × 12"),
      PolicyMethod.PercentOfNormal => (normal * v / 100m, $"{Rupees.Number(v)} % of the normal {Rupees.Text(normal)}"),
      PolicyMethod.PerRoom => (v * rooms, $"{rooms} rooms × {Rupees.Text(v)}"),
      PolicyMethod.PerSqft => (v * sqft, $"{Rupees.Number(sqft)} sq ft × {Rupees.Text(v)}"),
      PolicyMethod.PerKanal => (v * kanal, $"{Rupees.Number(kanal)} kanal × {Rupees.Text(v)}"),
      _ => (v, $"fixed {Rupees.Text(v)} a year")
    };

    if (policy.Method != PolicyMethod.PercentOfNormal && policy.AnnualIncrease && head.AppliesAnnualIncrease)
    {
      var years = Math.Max(0, fy - Math.Max(rateSet.BaseFiscalYear, FiscalYear.Of(policy.EffectiveFrom)));
      if (years > 0)
      {
        amount *= Compound(settings.AnnualIncreasePct, years);
        text += $", +{Rupees.Number(settings.AnnualIncreasePct)} % for {years} yr";
      }
    }
    return (Rupees.Round(amount), $"special policy \"{policy.Name}\": {text}");
  }

  /// The amount of an admin-defined head for one property, before the year share, overrides and the increase. Null when
  /// the head does not apply (a CUSTOM quantity not entered).
  public static (decimal Amount, string Explanation, decimal? Quantity, string? Unit, RateBand Row)? HeadAmount(TaxHead head, IReadOnlyList<RateBand> rows, Quantities q, decimal? customQty,
      IReadOnlyDictionary<string, decimal> vars)
  {
    var first = rows[0];
    var qty = q.For(first.Basis, customQty);
    var unit = UnitText(first);
    if (head.CalcMethod != CalcMethod.Fixed && first.Basis == RateBasis.Custom && !(qty > 0))
      return null;

    switch (head.CalcMethod)
    {
      case CalcMethod.Bands:
      {
        var x = first.RoundUp ? Math.Ceiling(qty) : qty;
        var (amount, row, note) = BandAbove(rows, x);
        return (amount, $"{Rupees.Number(qty)} {unit}{(x != qty ? $" (rounded up to {Rupees.Number(x)})" : "")}{(note.Length > 0 ? "; " + note : "")}", qty, UnitCode(first), row);
      }
      case CalcMethod.Rate:
      {
        // round up: a part unit counts as a whole one (3.2 rooms are charged as 4)
        var x = first.RoundUp ? Math.Ceiling(qty) : qty;
        return (first.Rate * x, $"{Rupees.Number(qty)} {unit}{(x != qty ? $" (rounded up to {Rupees.Number(x)})" : "")} × {Rupees.Number(first.Rate)}", qty, UnitCode(first), first);
      }
      case CalcMethod.Fixed:
        return (first.Rate, "fixed amount a year", null, null, first);
      case CalcMethod.Formula:
      {
        var formula = TaxFormula.Parse(head.FormulaFor(q.Category) ?? throw new DomainException($"{head.Name} has no formula for this category."));
        var withQty = new Dictionary<string, decimal>(vars) { ["QTY"] = qty };
        return (formula.Evaluate(withQty), formula.Expression + (first.Basis == RateBasis.Fixed ? "" : $" (QTY = {Rupees.Number(qty)} {unit})"), first.Basis == RateBasis.Fixed ? null : qty, UnitCode(first), first);
      }
      default:
        return null;
    }
  }

  /// The band a value falls in: the first whose upper limit is at or above it (the open band takes the rest).
  public static LtfFactor? Pick(IReadOnlyList<LtfFactor> bands, decimal x) =>
      bands.Count == 0 ? null : bands.FirstOrDefault(b => b.BandTo is null || x <= b.BandTo) ?? bands[^1];

  /// A band table with the rule above its top band: the top amount stays, or it repeats for each further block.
  public static (decimal Amount, RateBand Row, string Note) BandAbove(IReadOnlyList<RateBand> rows, decimal x)
  {
    var top = rows[^1];
    if (top.BandTo is null || x <= top.BandTo)
    {
      var band = rows.FirstOrDefault(b => b.BandTo is null || x <= b.BandTo) ?? top;
      return (band.Rate, band, "");
    }
    if (top.AboveTopRule != AboveTopRule.RepeatPerBlock)
      return (top.Rate, top, "above the top band: top rate stays");
    var block = top.BlockSize is > 0 ? top.BlockSize.Value : 1m;
    var extra = Math.Ceiling((x - top.BandTo.Value) / block);
    return (top.Rate * (1 + extra), top, $"above the top band: top rate + {extra} further block(s) of {Rupees.Number(block)}");
  }

  /// (1 + pct/100)^years, in decimals.
  public static decimal Compound(decimal pct, int years)
  {
    var factor = 1m;
    for (var i = 0; i < years; i++)
      factor *= 1 + pct / 100m;
    return factor;
  }

  private static string UnitText(RateBand row) => row.Basis switch
  {
    RateBasis.Rooms => "rooms",
    RateBasis.Kanal => "kanal",
    RateBasis.Marla => "marla",
    RateBasis.Sqft => "sq ft covered",
    RateBasis.Sqyd => "sq yd of land",
    RateBasis.Custom => row.UnitLabel ?? "units",
    _ => ""
  };

  private static string? UnitCode(RateBand row) => row.Basis switch
  {
    RateBasis.Fixed => null,
    RateBasis.Custom => "CUSTOM",
    _ => EnumText.UpperSnake(row.Basis)
  };
}

/// The quantities of a property the heads are charged on.
public sealed record Quantities(decimal LandSqyd, decimal CoveredSqft, decimal Kanal, decimal Marla, decimal Rooms, TaxCategory Category)
{
  public static Quantities Of(TaxFile file, TaxFileBasis basis)
  {
    var kanal = basis.TaxableKanal ?? 0m;
    return new Quantities(basis.TaxableLandSqyd ?? 0m, basis.CoveredAreaSqft ?? 0m, kanal, Math.Round(kanal * 20m, 2), file.Rooms ?? 0, file.TaxCategory);
  }

  public decimal For(RateBasis basis, decimal? customQty) => basis switch
  {
    RateBasis.Rooms => Rooms,
    RateBasis.Kanal => Kanal,
    RateBasis.Marla => Marla,
    RateBasis.Sqft => CoveredSqft,
    RateBasis.Sqyd => LandSqyd,
    RateBasis.Custom => customQty ?? 0m,
    _ => 0m
  };

  public IReadOnlyDictionary<string, decimal> Variables(decimal ltfLand, decimal ltfCovered) => new Dictionary<string, decimal>
  {
    ["A"] = LandSqyd,
    ["B"] = CoveredSqft,
    ["LTF_LAND"] = ltfLand,
    ["LTF_COVERED"] = ltfCovered,
    ["KANAL"] = Kanal,
    ["MARLA"] = Marla,
    ["ROOMS"] = Rooms
  };
}
