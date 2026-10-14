using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

/// The starting configuration, from the rates the Taxation Section presented and the rules of the recordings of
/// 12 Oct 2026 (the Tax Dummy prototype's defaults).
///
/// Always ensured: the heads the module refers to by code, a template for every notice stage, the two penalty rules, a
/// letterhead and a notice table layout. First run only: the two circles, the other heads and fees, the SMS texts, the
/// Section's special-policy proposals (inactive) and a DRAFT rate set with the conservancy, water and fee rates.
/// Towns and locality factors are not seeded: towns are Property's master, so they are assigned to circles (and their
/// factors entered) once Property has them; then the rate set is approved.
public sealed class TaxationSeeder(ApplicationDbContext context, ILogger<TaxationSeeder> logger)
{
  private static readonly DateOnly Start = new(2025, 7, 1);

  public async Task SeedAsync(CancellationToken cancellationToken = default)
  {
    var firstRun = !await context.TaxHeads.AnyAsync(cancellationToken);

    await EnsureHeadsAsync(firstRun, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    if (firstRun)
      await SeedFirstRunAsync(cancellationToken);

    await EnsureNoticeSetupAsync(cancellationToken);
    await EnsurePenaltyRulesAsync(cancellationToken);
    await context.SaveChangesAsync(cancellationToken);
  }

  // ---- heads ----

  private async Task EnsureHeadsAsync(bool firstRun, CancellationToken cancellationToken)
  {
    var codes = (await context.TaxHeads.Select(h => h.Code).ToListAsync(cancellationToken)).ToHashSet();
    void Add(TaxHead head)
    {
      if (codes.Add(head.Code))
        context.TaxHeads.Add(head);
    }

    // built in and system heads: always present
    Add(TaxHead.Builtin(TaxHeadId.New(), HeadCodes.PropertyTax, "Property tax", CalcMethod.Ltf, increase: true, overrideAllowed: true, sortOrder: 1,
      formula: "(A * LTF_LAND) + (B * LTF_COVERED)"));
    Add(TaxHead.Builtin(TaxHeadId.New(), HeadCodes.Rent, "Annual rent", CalcMethod.Agreement, increase: false, overrideAllowed: false, sortOrder: 9));
    Add(TaxHead.Other(TaxHeadId.New(), HeadCodes.LeaseMoney, "Lease money / ground rent", false, 20));
    Add(TaxHead.Other(TaxHeadId.New(), HeadCodes.Security, "Security", true, 21));
    Add(TaxHead.Other(TaxHeadId.New(), HeadCodes.Surcharge, "Surcharge / penalty", false, 22));
    Add(TaxHead.Other(TaxHeadId.New(), HeadCodes.Wht236A, "With holding FBR (236-A)", false, 23));
    Add(TaxHead.Other(TaxHeadId.New(), HeadCodes.Misc, "Other charge", false, 29));

    if (!firstRun)
      return;

    // the two configured regular heads of the Section's sheets (their rates are in the rate set)
    Add(TaxHead.Regular(TaxHeadId.New(), HeadCodes.Conservancy, new RegularHeadInput("Conservancy charges", CalcMethod.Bands, AppliesTo.Both, ChargedFrom.Registration,
      null, null, null, AppliesAnnualIncrease: true, OverrideAllowed: true, SortOrder: 2, IsActive: true, "BOA 15.06.2023", Start, null)));
    Add(TaxHead.Regular(TaxHeadId.New(), HeadCodes.Water, new RegularHeadInput("Water charges", CalcMethod.Rate, AppliesTo.Both, ChargedFrom.Facility,
      "water connection", null, null, AppliesAnnualIncrease: true, OverrideAllowed: true, SortOrder: 3, IsActive: true, "BOA 15.06.2023", Start, null)));

    // case-by-case fees
    Add(TaxHead.Other(TaxHeadId.New(), HeadCodes.TransferFee, "Transfer fee", false, 40));
    Add(TaxHead.Other(TaxHeadId.New(), HeadCodes.BpaFee, "Building plan approval fee", false, 41));
    Add(TaxHead.Other(TaxHeadId.New(), HeadCodes.BpaViolation, "Building plan violation fee", false, 42));
    Add(TaxHead.Other(TaxHeadId.New(), HeadCodes.NonUtilization, "Non-utilization fee", false, 43));
    Add(TaxHead.Other(TaxHeadId.New(), HeadCodes.WaterConnection, "Water connection fee", true, 44));
    Add(TaxHead.Other(TaxHeadId.New(), HeadCodes.MalbaSecurity, "Malba security", true, 45));
    Add(TaxHead.Other(TaxHeadId.New(), HeadCodes.Enlistment, "Contractor enlistment fee", false, 46));
    Add(TaxHead.Other(TaxHeadId.New(), HeadCodes.Registration, "Registration fee", false, 47));
    Add(TaxHead.Other(TaxHeadId.New(), HeadCodes.Licence, "Licence / permit fee", false, 48));
    Add(TaxHead.Other(TaxHeadId.New(), HeadCodes.DocumentCopies, "Document copies", false, 49));
  }

  // ---- first run ----

  private async Task SeedFirstRunAsync(CancellationToken cancellationToken)
  {
    var heads = await context.TaxHeads.ToDictionaryAsync(h => h.Code, cancellationToken);

    context.TaxCircles.Add(TaxCircle.Create(TaxCircleId.New(), "C-I", new CircleDetails("Circle I", null, null,
      "Taxation Office (Circle-I) 2nd Floor ZTBL Building Mansehra Road Abbottabad", "+92-992-9311081", "+92-992-9310240", 1)));
    context.TaxCircles.Add(TaxCircle.Create(TaxCircleId.New(), "C-II", new CircleDetails("Circle II", null, null,
      "Taxation Office (Circle-II) 2nd Floor ZTBL Building Mansehra Road Abbottabad", "+92-992-9311081", "+92-992-9310240", 2)));

    foreach (var (code, body) in new (string, string)[]
    {
      (MessageCodes.NoticeFirst, "Dear {name}, your GDA tax for FY {fy} on {code} is {total} (incl. arrears {arrears}). Ref {psid}. Pay via 1Link by {replyBy}."),
      (MessageCodes.NoticeSecond, "Dear {name}, second notice: {balance} on {code} (ref {psid}) is still unpaid. Please deposit by {replyBy}."),
      (MessageCodes.NoticeFinal, "Dear {name}, FINAL notice: {balance} on {code} (ref {psid}) remains unpaid. Deposit by {replyBy} to avoid action."),
      (MessageCodes.ShowCause, "Dear {name}, show-cause notice issued for {code}: {balance} unpaid. Reply by {replyBy} or the property may be sealed."),
      (MessageCodes.Penalty, "Dear {name}, a surcharge of {surcharge} has been added to {code}. Amount behind ref {psid} is now {balance}."),
      (MessageCodes.YearEnd, "Dear {name}, FY {fy} closed with {balance} unpaid on {code}; a {pct}% surcharge has been added and carried to arrears. Ref {psid}."),
      (MessageCodes.PaymentReceived, "Dear {name}, GDA has received {amount} against ref {psid} for {code}. Balance {balance}. Thank you."),
      (MessageCodes.Clearance, "Dear {name}, GDA has issued {purpose} clearance {no} for {code}, valid until {valid}.")
    })
      context.MessageTemplates.Add(MessageTemplate.Create(MessageTemplateId.New(), code, MessageChannel.Sms, body));

    // the Section's special-policy proposals (recording of 12 Oct 2026, Part 1), inactive until the admin turns them on
    foreach (var (code, name, head, method, value, appliesTo, increase, reference) in new (string, string, string, PolicyMethod, decimal, AppliesTo, bool, string)[]
    {
      ("HOTEL_MONTHLY", "Large hotel: monthly lump sum", HeadCodes.PropertyTax, PolicyMethod.PerMonth, 100000, AppliesTo.Com, true, "Section proposal (Part 1 04:33)"),
      ("DOUBLE_RATE", "Double the normal property tax", HeadCodes.PropertyTax, PolicyMethod.PercentOfNormal, 200, AppliesTo.Both, false, "Example 'doubled' (Part 1 17:44)"),
      ("HOTEL_CONS_ROOM", "Hotels: conservancy per room", HeadCodes.Conservancy, PolicyMethod.PerRoom, 1800, AppliesTo.Com, true, "Section proposal"),
      ("HOTEL_WATER_ROOM", "Hotels: water at a higher room rate", HeadCodes.Water, PolicyMethod.PerRoom, 3000, AppliesTo.Com, true, "Section proposal"),
      ("RES_WATER_FLAT", "Residential water: flat rate", HeadCodes.Water, PolicyMethod.Fixed, 12000, AppliesTo.Res, true, "Section proposal")
    })
      context.SpecialPolicies.Add(SpecialPolicy.Create(SpecialPolicyId.New(), heads[head],
        new SpecialPolicyInput(code, name, method, value, appliesTo, increase, Start, null, reference, IsActive: false)));

    // the rates of the Section's presentation, as a draft: towns and their locality factors are added, then it is approved
    var set = RateSet.Create(RateSetId.New(), "Initial tariff (BOA 15.06.2023)", "BOA 2013, 15.06.2019, 15.06.2023", new DateOnly(2023, 6, 15), Start);
    set.SetHeadRates(heads[HeadCodes.Conservancy], TaxCategory.Com, null, new HeadRateInput(RateBasis.Rooms, null,
      [new(5, 12000), new(10, 18000), new(15, 24000), new(20, 36000)], 5, false, null, null, null, AboveTopRule.TopRateStays));
    set.SetHeadRates(heads[HeadCodes.Conservancy], TaxCategory.Res, null, new HeadRateInput(RateBasis.Kanal, null,
      [new(1, 8000), new(2, 10000), new(3, 12000), new(4, 14000)], 1, true, null, null, null, AboveTopRule.TopRateStays));
    set.SetHeadRates(heads[HeadCodes.Water], TaxCategory.Com, null, new HeadRateInput(RateBasis.Rooms, null, null, null, false, 2000, null, null, null));
    set.SetHeadRates(heads[HeadCodes.Water], TaxCategory.Res, null, new HeadRateInput(RateBasis.Kanal, null, null, null, false, 10000, null, null, null));

    HeadRateInput Fee(RateBasis basis, decimal rate, RateType type) => new(basis, null, null, null, false, rate, null, type, null);
    set.SetHeadRates(heads[HeadCodes.BpaFee], TaxCategory.Com, null, Fee(RateBasis.Sqft, 60, RateType.PerUnit));
    set.SetHeadRates(heads[HeadCodes.BpaFee], TaxCategory.Res, null, Fee(RateBasis.Sqft, 30, RateType.PerUnit));
    set.SetHeadRates(heads[HeadCodes.BpaViolation], null, null, Fee(RateBasis.Sqft, 300, RateType.PerUnit));
    set.SetHeadRates(heads[HeadCodes.NonUtilization], TaxCategory.Com, null, Fee(RateBasis.Marla, 6000, RateType.PerUnit));
    set.SetHeadRates(heads[HeadCodes.NonUtilization], TaxCategory.Res, null, Fee(RateBasis.Marla, 3000, RateType.PerUnit));
    set.SetHeadRates(heads[HeadCodes.MalbaSecurity], null, null, Fee(RateBasis.Fixed, 200000, RateType.Flat));
    set.SetHeadRates(heads[HeadCodes.Enlistment], null, null, Fee(RateBasis.Fixed, 10000, RateType.Flat));
    set.SetHeadRates(heads[HeadCodes.Registration], TaxCategory.Com, null, Fee(RateBasis.PercentOfValue, 4, RateType.Percent));
    set.SetHeadRates(heads[HeadCodes.Registration], TaxCategory.Res, null, Fee(RateBasis.PercentOfValue, 2, RateType.Percent));
    set.SetHeadRates(heads[HeadCodes.Licence], null, null, Fee(RateBasis.Fixed, 5000, RateType.Flat));
    set.SetHeadRates(heads[HeadCodes.WaterConnection], null, null, Fee(RateBasis.Fixed, 15000, RateType.Flat));
    set.SetHeadRates(heads[HeadCodes.DocumentCopies], null, null, Fee(RateBasis.Page, 100, RateType.PerUnit));
    context.RateSets.Add(set);

    await context.SaveChangesAsync(cancellationToken);
    logger.LogInformation("Seeded the taxation circles, heads, SMS texts, special-policy proposals and a draft rate set.");
  }

  // ---- notices ----

  private async Task EnsureNoticeSetupAsync(CancellationToken cancellationToken)
  {
    var stages = (await context.NoticeTemplates.Select(t => t.Stage).ToListAsync(cancellationToken)).ToHashSet();
    foreach (var (stage, name, source, title, paragraph) in Templates)
      if (stages.Add(stage))
        context.NoticeTemplates.Add(NoticeTemplate.Create(NoticeTemplateId.New(), stage, name, title, paragraph, source, Start));

    if (!await context.Letterheads.AnyAsync(cancellationToken))
      context.Letterheads.Add(NoticeLetterhead.Create(NoticeLetterheadId.New(), new LetterheadInput(
        "GOVERNMENT OF KHYBER PAKHTUNKHWA",
        "GALIYAT DEVELOPMENT AUTHORITY ABBOTTABAD",
        "TOGETHER WE MAKE BETTER AND PROSPEROUS GALIYAT AND PAKISTAN",
        [
          "Errors and Omission are acceptable.",
          "About the tax valuation / assessment do not hesitate to contact us via phone or Address given above, and if you have already paid the tax, please ignore this letter and send us a copy of the payment.",
          "This notice shall not be accepted /Produce as evidence in any court of law to claim the landed area and status of land i-e residential or commercial."
        ],
        ["The Deputy Director (Taxation) , GDA Abbottabad.", "The Concern Tax Collector, GDA Abbottabad for necessary action.", "Record Keeper."],
        "Please Scan and pay the Amount With your mobile Banking App.",
        "Allied Bank",
        Watermark: true,
        "The Section's notice of 16/09/2026"), null, null, Start));

    if (!await context.TableLayouts.AnyAsync(cancellationToken))
    {
      var known = (await context.TaxHeads.Select(h => h.Code).ToListAsync(cancellationToken)).ToHashSet();
      TableRowInput Row(string label, params string[] codes) => new(label, codes.Where(known.Contains).ToList());
      var rows = new[]
      {
        Row("Property Tax", HeadCodes.PropertyTax),
        Row("Water Charges", HeadCodes.Water),
        Row("Conservancy Charges", HeadCodes.Conservancy),
        Row("Annual Rent / G .Rent", HeadCodes.Rent, HeadCodes.LeaseMoney),
        Row("Security", HeadCodes.Security),
        Row("Surcharge on late payment", HeadCodes.Surcharge),
        Row("With Holding FBR(236-A)", HeadCodes.Wht236A)
      }.Where(r => r.HeadCodes.Count > 0).ToList();
      context.TableLayouts.Add(NoticeTableLayout.Create(NoticeTableLayoutId.New(),
        new TableLayoutInput(rows, [], "Other charges / earlier dues", false, "The Section's notice of 16/09/2026"), known, Start));
    }
  }

  private static readonly (NoticeStage Stage, string Name, string Source, string Title, string Paragraph)[] Templates =
  [
    (NoticeStage.First, "First notice of demand", "following the Section's 2nd notice (16/09/2026); the 1st notice PDF is awaited", "1st NOTICE REGARDING PAYMENT OF TAX/DUES",
      "This letter is to inform you that the tax amounting to {amount} for the financial year {fy} is due against you. Please pay it within {days} after issuance of this letter. Being a responsible citizen, we may expect that you will deposit the dues within stipulated time. In case of non-payment within due time, further notices will be issued and an overdue surcharge may be levied against you."),
    (NoticeStage.Second, "Second notice", "the Section's notice of 16/09/2026", "2nd NOTICE REGARDING CLEARANCE OF ARREARS OF TAX/DUES",
      "This letter is to remind that the tax amounting to {amount} is outstanding against you. Please pay it within {days} after issuance of this letter. Being a responsible citizen, we may expect that you will deposit the dues within stipulated time. In case of non-payment within due time, an overdue surcharge will be levied against you. Therefore, in order to avoid any inconvenience please forward the payment within due time, otherwise Authority will take strict action in accordance with the law."),
    (NoticeStage.Final, "Final notice", "following the Section's 2nd notice; the final notice PDF is awaited", "FINAL NOTICE REGARDING CLEARANCE OF ARREARS OF TAX/DUES",
      "Despite earlier notices, the tax amounting to {amount} is still outstanding against you. This is the final notice: please pay it within {days} after issuance of this letter, failing which a show cause notice will be issued, an overdue surcharge will be levied and the Authority will take strict action in accordance with the law."),
    (NoticeStage.ShowCause, "Show-cause notice", "following the Section's 2nd notice; the show cause PDF is awaited", "SHOW CAUSE NOTICE FOR NON-PAYMENT OF TAX/DUES",
      "Whereas the tax amounting to {amount} is outstanding against you despite repeated notices, you are hereby called upon to show cause within {days} after issuance of this letter why action, including sealing of the property, should not be taken against you in accordance with the law."),
    (NoticeStage.Surcharge, "Surcharge notice", "proposed (not one of the Section's four)", "NOTICE OF SURCHARGE ON LATE PAYMENT OF TAX/DUES",
      "A surcharge of {surcharge} has been levied on the outstanding tax under the Authority's decision. The amount now outstanding against you is {amount}. Please pay it within {days} after issuance of this letter to avoid further action in accordance with the law.")
  ];

  // ---- penalties ----

  private async Task EnsurePenaltyRulesAsync(CancellationToken cancellationToken)
  {
    var kinds = (await context.PenaltyRules.Select(r => r.Kind).ToListAsync(cancellationToken)).ToHashSet();
    if (kinds.Add(PenaltyKind.IntraYear))
      context.PenaltyRules.Add(PenaltyRule.Create(PenaltyRuleId.New(), "INTRA_YEAR_SURCHARGE", "In-year surcharge", PenaltyKind.IntraYear, PenaltyTrigger.Manual,
        new PenaltyRuleInput(null, PenaltyRateType.Percent, 2, TargetSet.DefaultersOnly, true, true, 3, PeriodUnit.Months, PeriodAnchor.FyStart, null), Start));
    if (kinds.Add(PenaltyKind.YearEnd))
      context.PenaltyRules.Add(PenaltyRule.Create(PenaltyRuleId.New(), "YEAR_END_10", "Year-end surcharge", PenaltyKind.YearEnd, PenaltyTrigger.Auto,
        new PenaltyRuleInput(null, PenaltyRateType.Percent, 10, TargetSet.AllUnpaid, true, true, null, null, null, null), Start));
  }
}
