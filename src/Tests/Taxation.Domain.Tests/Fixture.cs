/// Builders for what most tests need: the heads, an approved rate set with one town's locality factors (the Section's
/// Nathiagali rates), the settings, and a tax file opened from a Property snapshot.
internal static class Fixture
{
  public static readonly Guid Officer = Guid.Parse("11111111-1111-1111-1111-111111111111");
  public static readonly Guid Town = Guid.Parse("22222222-2222-2222-2222-222222222222");
  public static readonly DateTime Now = new(2026, 10, 14, 6, 0, 0, DateTimeKind.Utc);

  public static DateOnly D(string iso) => DateOnly.Parse(iso);

  public static readonly TaxSettings Settings = TaxSettings.From(new Dictionary<string, string>());

  public static readonly TaxHead PropertyTax = TaxHead.Builtin(TaxHeadId.New(), HeadCodes.PropertyTax, "Property tax", CalcMethod.Ltf, true, true, 1, "(A * LTF_LAND) + (B * LTF_COVERED)");
  public static readonly TaxHead Rent = TaxHead.Builtin(TaxHeadId.New(), HeadCodes.Rent, "Annual rent", CalcMethod.Agreement, false, false, 9);
  public static readonly TaxHead Conservancy = TaxHead.Regular(TaxHeadId.New(), HeadCodes.Conservancy, new RegularHeadInput("Conservancy charges", CalcMethod.Bands,
    AppliesTo.Both, ChargedFrom.Registration, null, null, null, true, true, 2, true, null, null, null));
  public static readonly TaxHead Water = TaxHead.Regular(TaxHeadId.New(), HeadCodes.Water, new RegularHeadInput("Water charges", CalcMethod.Rate,
    AppliesTo.Both, ChargedFrom.Facility, "water connection", null, null, true, true, 3, true, null, null, null));
  public static readonly TaxHead Surcharge = TaxHead.Other(TaxHeadId.New(), HeadCodes.Surcharge, "Surcharge / penalty", false, 22, chargedOnNotice: true);
  public static readonly TaxHead Lease = TaxHead.Other(TaxHeadId.New(), HeadCodes.LeaseMoney, "Lease money / ground rent", false, 20, chargedOnNotice: true);
  public static readonly TaxHead TransferFee = TaxHead.Other(TaxHeadId.New(), HeadCodes.TransferFee, "Transfer fee", false, 40);

  public static IReadOnlyList<TaxHead> Heads => [PropertyTax, Conservancy, Water, Rent, Surcharge, Lease, TransferFee];

  public static IReadOnlyDictionary<TaxHeadId, TaxHead> HeadsById => Heads.ToDictionary(h => h.Id);

  public static IReadOnlyDictionary<string, string> HeadNames => Heads.ToDictionary(h => h.Code, h => h.Name);

  /// The Section's tariff from 1 July 2025 with Nathiagali's factors; conservancy above the top band as given.
  public static RateSet RateSet(AboveTopRule rule = AboveTopRule.TopRateStays)
  {
    var set = global::RateSet.Create(RateSetId.New(), "BOA 15.06.2023", "BOA 15.06.2023", null, D("2025-07-01"));
    set.SetLtfFactors(Town, TaxCategory.Res, LtfComponent.Land, [new(500, 5), new(null, 6)]);
    set.SetLtfFactors(Town, TaxCategory.Res, LtfComponent.Covered, [new(3000, 5), new(null, 6)]);
    set.SetLtfFactors(Town, TaxCategory.Com, LtfComponent.Land, [new(310, 10), new(null, 12)]);
    set.SetLtfFactors(Town, TaxCategory.Com, LtfComponent.Covered, [new(1500, 14), new(3000, 16), new(4500, 18), new(6000, 20), new(7500, 22), new(null, 24)]);
    set.SetHeadRates(Conservancy, TaxCategory.Com, null, new HeadRateInput(RateBasis.Rooms, null, [new(5, 12000), new(10, 18000), new(15, 24000), new(20, 36000)], 5, false, null, null, null, rule));
    set.SetHeadRates(Conservancy, TaxCategory.Res, null, new HeadRateInput(RateBasis.Kanal, null, [new(1, 8000), new(2, 10000), new(3, 12000), new(4, 14000)], 1, true, null, null, null, rule));
    set.SetHeadRates(Water, TaxCategory.Com, null, new HeadRateInput(RateBasis.Rooms, null, null, null, false, 2000, null, null, null));
    set.SetHeadRates(Water, TaxCategory.Res, null, new HeadRateInput(RateBasis.Kanal, null, null, null, false, 10000, null, null, null));
    set.Approve(Officer, Now, Heads, [(Town, "NATHIAGALI")], []);
    return set;
  }

  /// A property of 400 sq yd (3,600 sq ft) with 2,500 sq ft covered, one owner.
  public static PropertySnapshot Snapshot(decimal landSqft = 3600, decimal? coveredSqft = 2500, DateOnly? bpaDate = null, DateOnly? completionDate = null,
      IReadOnlyList<AgreementSnapshot>? agreements = null) =>
    new(Guid.NewGuid(), "PROP-00001", "Test property", true, Town, "NATHIAGALI", "Nathiagali", Guid.NewGuid(), "RESIDENTIAL", "OCCUPIED", "PRIVATE_PROPERTY",
      "Mall Road, Nathiagali", landSqft, landSqft / 9, "SQYD", null, coveredSqft, coveredSqft is null ? CoveredAreaSource.None : CoveredAreaSource.ApprovedPlan,
      Guid.NewGuid(), Guid.NewGuid(), bpaDate, completionDate is null ? null : Guid.NewGuid(), completionDate, HoldingKind.PrivateRegistered,
      [new PayerSnapshot(Guid.NewGuid(), "OWN-00001", PayerRole.Owner, PayerSource.Ownership, Guid.NewGuid(), 100, "Asif Jadoon", null, "35202-1234567-1", "0300-1234567",
        "Chairlift Road, Nathiagali", D("2020-01-01"))],
      agreements ?? [], "v1");

  public static TaxFile File(TaxCategory category = TaxCategory.Res, int? rooms = null, DateOnly? registeredOn = null, PropertySnapshot? snapshot = null,
      UpliftRateBasis? upliftBasis = null, decimal? upliftPct = null) =>
    TaxFile.Open(TaxFileId.New(), "GDA-NATHIAGALI-RSD-001", "601100000001", snapshot ?? Snapshot(), TaxCircleId.New(), category,
      new TaxFileDetails(rooms, registeredOn ?? D("2015-01-01"), upliftBasis, upliftPct, "NA/R/01", "64", null), D("2025-07-01"), Now);

  public static Assessment Assess(TaxFile file, int fiscalYear, RateSet? rateSet = null, IReadOnlyList<SpecialPolicy>? policies = null, IReadOnlyList<TaxHead>? heads = null) =>
    TaxCalculator.Assess(new AssessmentInput(file, file.CurrentBasis!, fiscalYear, heads ?? Heads, rateSet ?? RateSet(), Settings, policies ?? []));

  public static decimal Line(Assessment a, TaxHead head) => a.Lines.Single(l => l.HeadId == head.Id).Amount;

  /// A demand issued on 1 July of the year with the year's computation.
  public static Demand Issued(TaxFile file, int fiscalYear, IReadOnlyList<ComputedLine>? lines = null)
  {
    var demand = Demand.Draft(DemandId.New(), $"DMD-{fiscalYear}", null, file, file.CurrentBasis!, file.Addressee(D("2025-07-01"))!, fiscalYear,
      lines ?? Assess(file, fiscalYear).Lines, 0);
    demand.Issue(FiscalYear.Start(fiscalYear), Settings.DueDate(fiscalYear), []);
    return demand;
  }

  public static PenaltyRule YearEndRule() => PenaltyRule.Create(PenaltyRuleId.New(), "YEAR_END_10", "Year-end surcharge", PenaltyKind.YearEnd, PenaltyTrigger.Auto,
    new PenaltyRuleInput(null, PenaltyRateType.Percent, 10, TargetSet.AllUnpaid, true, true, null, null, null, null), D("2025-07-01"));

  public static PenaltyRule IntraYearRule() => PenaltyRule.Create(PenaltyRuleId.New(), "INTRA_YEAR_SURCHARGE", "In-year surcharge", PenaltyKind.IntraYear, PenaltyTrigger.Manual,
    new PenaltyRuleInput(null, PenaltyRateType.Percent, 2, TargetSet.DefaultersOnly, true, true, 3, PeriodUnit.Months, PeriodAnchor.FyStart, null), D("2025-07-01"));

  /// The Section's rows; lease money is left without a row (as a head deactivated since), so it prints under its own name.
  public static NoticeTableLayout Layout(bool hideZero = false)
  {
    TableRowInput[] rows =
    [
      new("Property Tax", [HeadCodes.PropertyTax]),
      new("Water Charges", [HeadCodes.Water]),
      new("Conservancy Charges", [HeadCodes.Conservancy]),
      new("Annual Rent / G .Rent", [HeadCodes.Rent]),
      new("Surcharge on late payment", [HeadCodes.Surcharge])
    ];
    return NoticeTableLayout.Create(NoticeTableLayoutId.New(), new TableLayoutInput(rows, [], hideZero, "test"), HeadNames,
      rows.SelectMany(r => r.HeadCodes).ToList(), D("2025-07-01"));
  }

  public static IReadOnlyDictionary<Guid, decimal> NothingPaid => new Dictionary<Guid, decimal>();
}
