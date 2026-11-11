using static Fixture;

/// The annual computation (TaxCalculator), one rule per test, with the Section's Nathiagali rates.
public class AssessmentTests
{
  [Fact]
  public void Property_tax_is_land_times_its_factor_plus_covered_area_times_its_factor()
  {
    // 400 sq yd x 5 + 2,500 sq ft x 5
    var a = Assess(File(), 2025);
    Assert.Equal(14500m, Line(a, PropertyTax));
    Assert.Contains("A=400 sq yd × 5", a.Lines.Single(l => l.HeadId == PropertyTax.Id).Explanation);
  }

  [Fact]
  public void A_band_takes_the_value_at_or_below_its_limit_and_the_open_band_the_rest()
  {
    // 600 sq yd is above the 500 sq yd band: factor 6; 3,000 sq ft is in the first covered band: factor 5
    var file = File(snapshot: Snapshot(landSqft: 5400, coveredSqft: 3000));
    Assert.Equal(600m * 6 + 3000m * 5, Line(Assess(file, 2025), PropertyTax));
  }

  [Fact]
  public void The_annual_increase_compounds_from_the_rate_set_year()
  {
    // FY 2027-28 is two years after the set's FY 2025-26: 14,500 x 1.1 x 1.1
    var line = Assess(File(), 2027).Lines.Single(l => l.HeadId == PropertyTax.Id);
    Assert.Equal(14500m, line.BaseAmount);
    Assert.Equal(3045m, line.IncreaseAmount);
    Assert.Equal(17545m, line.Amount);
  }

  [Fact]
  public void Rent_does_not_take_the_annual_increase()
  {
    var rental = new AgreementSnapshot(AgreementKind.Rental, Guid.NewGuid(), "RNT-036", 600000, AmountFrequency.Annual, D("2025-07-01"), null, null, 0, true);
    var file = File(snapshot: Snapshot(agreements: [rental]));
    var line = Assess(file, 2027).Lines.Single(l => l.HeadId == Rent.Id);
    Assert.Equal(0m, line.IncreaseAmount);
    Assert.Equal(600000m, line.Amount);
  }

  [Fact]
  public void Without_a_completion_certificate_after_the_grace_years_the_commercial_rate_applies()
  {
    // building plan Jan 2022, no completion: 3.5 years by 1 July 2025 > 2; residential at commercial factors and 100 %
    var file = File(snapshot: Snapshot(bpaDate: D("2022-01-01")));
    var a = Assess(file, 2025);
    Assert.Equal(TaxCategory.Com, a.CategoryUsed);
    Assert.Equal(400m * 12 + 2500m * 16, Line(a, PropertyTax));
    Assert.Contains(a.Notes, n => n.Contains("No completion certificate"));
  }

  [Fact]
  public void A_commercial_property_without_completion_pays_the_uplift_percentage()
  {
    var file = File(TaxCategory.Com, rooms: 4, snapshot: Snapshot(bpaDate: D("2022-01-01")));
    var line = Assess(file, 2025).Lines.Single(l => l.HeadId == PropertyTax.Id);
    var formula = 400m * 12 + 2500m * 16;
    Assert.Equal(formula, line.BaseAmount);
    Assert.Equal(formula * 0.5m, line.UpliftAmount);
    Assert.Equal(formula * 1.5m, line.Amount);
  }

  [Fact]
  public void A_completion_certificate_before_the_year_removes_the_uplift()
  {
    var file = File(snapshot: Snapshot(bpaDate: D("2022-01-01"), completionDate: D("2025-03-01")));
    Assert.Equal(14500m, Line(Assess(file, 2025), PropertyTax));
  }

  [Fact]
  public void Days_under_litigation_hold_do_not_count_toward_the_completion_grace()
  {
    // plan 1 June 2023: 2.08 years by 1 July 2025, but 100 held days take it under 2
    var file = File(snapshot: Snapshot(bpaDate: D("2023-06-01")));
    file.AddHold("Court case", null, null, D("2024-01-01"), suspendsDemand: false, excludesCompletionClock: true);
    file.EndHold(file.Holds[0].Id, D("2024-04-09"));
    var a = Assess(file, 2025);
    Assert.Equal(14500m, Line(a, PropertyTax));
    Assert.Contains(a.Notes, n => n.Contains("Completion clock"));
  }

  [Fact]
  public void An_open_litigation_hold_on_1_July_suspends_the_year()
  {
    var file = File();
    file.AddHold("Court case", "WP-12/2025", null, D("2025-05-01"), suspendsDemand: true, excludesCompletionClock: true);
    var a = Assess(file, 2025);
    Assert.True(a.Suspended);
    Assert.Empty(a.Lines);
  }

  [Fact]
  public void Conservancy_runs_from_the_registration_date_for_the_rest_of_the_year()
  {
    // 0.66 kanal counts as 1 kanal (round up): Rs 8,000 a year, from 1 Jan 2026: 181 of 365 days
    var a = Assess(File(registeredOn: D("2026-01-01")), 2025);
    Assert.Equal(Rupees.Round(8000m * 181 / 365), Line(a, Conservancy));
  }

  [Fact]
  public void Conservancy_is_not_charged_for_a_year_before_registration()
  {
    var a = Assess(File(registeredOn: D("2026-08-01")), 2025);
    Assert.DoesNotContain(a.Lines, l => l.HeadId == Conservancy.Id);
    Assert.Contains(a.Notes, n => n.Contains("after this year"));
  }

  [Fact]
  public void Water_is_charged_only_once_the_connection_is_taken()
  {
    var file = File();
    Assert.DoesNotContain(Assess(file, 2025).Lines, l => l.HeadId == Water.Id);

    file.SetHeadValue(Water, D("2025-07-01"), null);
    Assert.Equal(file.WaterFacilityFrom, D("2025-07-01"));
    Assert.Equal(Rupees.Round(file.CurrentBasis!.TaxableKanal!.Value * 10000m), Line(Assess(file, 2025), Water));
  }

  [Fact]
  public void Above_the_top_conservancy_band_the_top_rate_stays()
  {
    var file = File(TaxCategory.Com, rooms: 23);
    Assert.Equal(36000m, Line(Assess(file, 2025), Conservancy));
  }

  [Fact]
  public void Above_the_top_band_the_rate_can_repeat_for_each_further_block()
  {
    // 23 rooms: 3 above the top band of 20, one further block of 5 rooms
    var file = File(TaxCategory.Com, rooms: 23);
    Assert.Equal(72000m, Line(Assess(file, 2025, RateSet(AboveTopRule.RepeatPerBlock)), Conservancy));
  }

  [Fact]
  public void A_minimum_override_tops_the_computed_amount_up_to_the_floor()
  {
    var file = File();
    file.AddOverride(PropertyTax, OverrideMode.Minimum, null, 20000, true, "Section floor", null, D("2025-07-01"), Officer, Now);
    var line = Assess(file, 2025).Lines.Single(l => l.HeadId == PropertyTax.Id);
    Assert.Equal(LineMode.Minimum, line.Mode);
    Assert.Equal(5500m, line.OverrideAmount);
    Assert.Equal(20000m, line.Amount);
    Assert.Equal(14500m, line.NormalAmount);
  }

  [Fact]
  public void A_special_charge_replaces_the_formula_and_follows_the_increase_from_its_start()
  {
    var file = File(TaxCategory.Com, rooms: 40);
    file.AddOverride(PropertyTax, OverrideMode.Special, null, 1_200_000, followsIncrease: true, "Hotel lump sum", "Board 2025", D("2025-07-01"), Officer, Now);
    Assert.Equal(1_200_000m, Line(Assess(file, 2025), PropertyTax));
    Assert.Equal(1_320_000m, Line(Assess(file, 2026), PropertyTax));
  }

  [Fact]
  public void A_special_policy_charged_a_month_is_twelve_months_with_its_increase()
  {
    var policy = SpecialPolicy.Create(SpecialPolicyId.New(), PropertyTax,
      new SpecialPolicyInput("HOTEL_MONTHLY", "Large hotel: monthly lump sum", PolicyMethod.PerMonth, 100000, AppliesTo.Com, true, D("2025-07-01"), null, null, true));
    var file = File(TaxCategory.Com, rooms: 40);
    file.AddOverride(PropertyTax, OverrideMode.Special, policy, null, true, "Hotel", null, D("2025-07-01"), Officer, Now);
    var line = Assess(file, 2026, policies: [policy]).Lines.Single(l => l.HeadId == PropertyTax.Id);
    Assert.Equal(LineMode.Policy, line.Mode);
    Assert.Equal(1_320_000m, line.Amount);
  }

  [Fact]
  public void An_inactive_special_policy_leaves_the_normal_tax()
  {
    var policy = SpecialPolicy.Create(SpecialPolicyId.New(), PropertyTax,
      new SpecialPolicyInput(null, "Double", PolicyMethod.PercentOfNormal, 200, AppliesTo.Both, false, D("2025-07-01"), null, null, true));
    var file = File();
    file.AddOverride(PropertyTax, OverrideMode.Special, policy, null, true, "Double", null, D("2025-07-01"), Officer, Now);
    policy.SetActive(false);
    var a = Assess(file, 2025, policies: [policy]);
    Assert.Equal(14500m, Line(a, PropertyTax));
    Assert.Contains(a.Notes, n => n.Contains("not active"));
  }

  [Fact]
  public void Rent_steps_up_on_each_anniversary_by_the_step_in_force_then()
  {
    // base Rs 600,000 from 1 July 2022, +7 % every 2 years: by 1 July 2026 two steps
    var rental = new AgreementSnapshot(AgreementKind.Rental, Guid.NewGuid(), "RNT-036", 600000, AmountFrequency.Annual, D("2022-07-01"), null, null, 7, true);
    var file = File(snapshot: Snapshot(agreements: [rental]));
    file.Agreement(file.Agreements[0].Id).SetTerms(7, 2, false, null);
    Assert.Equal(686940m, Line(Assess(file, 2026), Rent));
  }

  [Fact]
  public void A_mid_term_revision_changes_later_steps_and_leaves_past_years()
  {
    // +15 % a year, revised to 7 % from 1 July 2024: 2023 and 2024 at 15 %, 2025 and 2026 at 7 %
    var rental = new AgreementSnapshot(AgreementKind.Rental, Guid.NewGuid(), "RNT-040", 100000, AmountFrequency.Annual, D("2022-07-01"), null, null, 15, true);
    var file = File(snapshot: Snapshot(agreements: [rental]));
    var agreement = file.Agreement(file.Agreements[0].Id);
    Assert.Equal(Rupees.Round(100000m * 1.15m * 1.15m), agreement.AmountFor(2024)!.Value.Amount);
    agreement.Revise(D("2025-07-01"), 7, 1, "Board 2025");
    Assert.Equal(Rupees.Round(100000m * 1.15m * 1.15m), agreement.AmountFor(2024)!.Value.Amount);
    Assert.Equal(Rupees.Round(100000m * 1.15m * 1.15m * 1.07m * 1.07m), agreement.AmountFor(2026)!.Value.Amount);
  }

  [Fact]
  public void A_monthly_rent_is_billed_for_twelve_months()
  {
    var rental = new AgreementSnapshot(AgreementKind.Rental, Guid.NewGuid(), "RNT-050", 25000, AmountFrequency.Monthly, D("2025-07-01"), null, null, 0, true);
    var file = File(snapshot: Snapshot(agreements: [rental]));
    Assert.Equal(300000m, Line(Assess(file, 2025), Rent));
  }

  [Fact]
  public void An_exempt_file_is_not_assessed()
  {
    var a = Assess(File(TaxCategory.Exempt), 2025);
    Assert.True(a.Suspended);
  }

  [Fact]
  public void A_property_without_land_area_cannot_be_assessed()
  {
    var file = File(snapshot: Snapshot(landSqft: 0) with { LandAreaSqft = null });
    var error = Assert.Throws<DomainException>(() => Assess(file, 2025));
    Assert.Contains("no land area", error.Message);
  }

  [Fact]
  public void A_formula_head_uses_the_property_variables_and_its_quantity()
  {
    var signboard = TaxHead.Regular(TaxHeadId.New(), "SIGNBOARD_FEE", new RegularHeadInput("Signboard fee", CalcMethod.Formula, AppliesTo.Com, ChargedFrom.Always,
      null, "max(5000, QTY * 60)", null, false, true, 5, true, null, null, null));
    var set = global::RateSet.Create(RateSetId.New(), "Copy", "Board", null, D("2025-07-01"));
    set.CopyRatesFrom(RateSet());
    set.SetHeadRates(signboard, TaxCategory.Com, null, new HeadRateInput(RateBasis.Custom, "sq ft of signboard", null, null, false, null, null, null, null));
    var heads = Heads.Append(signboard).ToList();
    set.Approve(Officer, Now, heads, [(Town, "NATHIAGALI")], []);

    var file = File(TaxCategory.Com, rooms: 10);
    Assert.DoesNotContain(Assess(file, 2025, set, heads: heads).Lines, l => l.HeadId == signboard.Id);
    file.SetHeadValue(signboard, null, 120);
    Assert.Equal(7200m, Line(Assess(file, 2025, set, heads: heads), signboard));
  }
}
