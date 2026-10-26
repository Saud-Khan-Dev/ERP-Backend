using static Fixture;

/// The rules added with the 2026-10 fixes: dates (due date, years not started or not over), notices (endorsement at
/// issue, review of date and days, nothing owed), surcharges once, credits and reversals, fees and clearances, and the
/// "void, not yet in force" acts.
public class MoneyAndTimingRulesTests
{
  // ---- dates ----

  [Fact]
  public void A_demand_issued_late_is_due_some_days_after_it_is_issued_never_before()
  {
    Assert.Equal(D("2025-08-31"), Settings.DueDate(2025, D("2025-07-01")));
    Assert.Equal(D("2025-11-18"), Settings.DueDate(2025, D("2025-10-19")));

    var file = File();
    var draft = Demand.Draft(DemandId.New(), "DMD-1", null, file, file.CurrentBasis!, file.Addressee(D("2025-07-01"))!, 2025, Assess(file, 2025).Lines, 0);
    Assert.Contains("before it is issued", Assert.Throws<DomainException>(() => draft.Issue(D("2025-10-19"), D("2025-08-31"), [])).Message);
  }

  [Fact]
  public void A_year_not_over_is_never_carried()
  {
    var file = File();
    var last = Issued(file, 2025);
    var next = Demand.Draft(DemandId.New(), "DMD-2026", null, file, file.CurrentBasis!, file.Addressee(D("2026-07-01"))!, 2026, Assess(file, 2026).Lines, 0);
    var error = Assert.Throws<DomainException>(() => last.CarryInto(next, NothingPaid, Surcharge.Id, Officer, Now, D("2026-06-30")));
    Assert.Contains("is not over until 30/06/2026", error.Message);
    Assert.Equal(DemandStatus.Issued, last.Status);
  }

  [Fact]
  public void A_run_for_a_year_that_has_not_started_is_worked_out_but_not_approved()
  {
    var run = DemandRun.Compute(DemandRunId.New(), 2025, null, RateSet(), Officer, Now);
    Assert.Contains("starts on 01/07/2025", Assert.Throws<DomainException>(() => run.Approve(Officer, Now, D("2025-06-30"))).Message);
    run.Approve(Officer, Now, D("2025-07-01"));
    Assert.Equal(DemandRunStatus.Approved, run.Status);
  }

  // ---- notices ----

  [Fact]
  public void The_endorsement_number_is_taken_when_a_notice_is_issued_and_a_notice_with_nothing_owed_is_not_issued()
  {
    var file = File();
    var demand = Issued(file, 2025);
    var notice = PrepareNotice(demand, file);
    Assert.Null(notice.EndstNo);

    demand.Settle(demand.Balance, NothingPaid, Surcharge.Id, D("2025-07-10"));
    var table = NoticeTable.Build(demand, demand.Items(NothingPaid, Surcharge.Id), HeadsById, Layout());
    Assert.Contains("Nothing is owed", Assert.Throws<DomainException>(() => notice.Confirm(demand, table, null, null, "TAX/C-I/2025-26/00001", Officer, Now)).Message);

    var open = Issued(file, 2026);
    var second = PrepareNotice(open, file);
    second.Confirm(open, NoticeTable.Build(open, open.Items(NothingPaid, Surcharge.Id), HeadsById, Layout()), null, null, "TAX/C-I/2026-27/00007", Officer, Now);
    Assert.Equal("TAX/C-I/2026-27/00007", second.EndstNo);
  }

  [Fact]
  public void A_reviewer_can_move_the_date_and_the_days_and_the_pay_by_date_moves_with_them()
  {
    var notice = PrepareNotice();
    notice.Change(new NoticeChanges(IssueDate: D("2025-07-08"), Days: 15, Reason: "printed late"), [], Officer, Now, movedReplyBy: D("2025-07-29"));
    Assert.Equal(D("2025-07-08"), notice.IssueDate);
    Assert.Equal(D("2025-07-29"), notice.ReplyBy);
    Assert.Equal(15, notice.Variables.Days);
    Assert.True(notice.DateEdited);

    notice.Change(new NoticeChanges(Paragraph: "Pay {amount} now.", Reason: "shorter"), [], Officer, Now);
    Assert.Equal("Pay {amount} now.", notice.Variables.Paragraph);
    Assert.Contains("Unknown placeholder", Assert.Throws<DomainException>(() =>
      notice.Change(new NoticeChanges(Paragraph: "Pay {nonsense}", Reason: "x"), [], Officer, Now)).Message);
  }

  [Fact]
  public void A_cycle_notice_confirmed_late_is_dated_the_day_it_goes_out_unless_its_date_was_set()
  {
    var notice = PrepareNotice();
    notice.RedateTo(D("2025-07-20"), D("2025-08-01"));
    Assert.Equal(D("2025-07-20"), notice.IssueDate);
    Assert.Equal(D("2025-08-01"), notice.ReplyBy);

    var set = PrepareNotice();
    set.Change(new NoticeChanges(IssueDate: D("2025-07-03"), ReplyBy: D("2025-07-20"), Reason: "as signed"), [], Officer, Now);
    set.RedateTo(D("2025-07-25"), D("2025-08-05"));
    Assert.Equal(D("2025-07-03"), set.IssueDate);
  }

  [Fact]
  public void The_next_notice_is_due_only_above_the_highest_stage_reached()
  {
    var settings = Settings with { CountWorkingDays = false };
    Assert.True(NoticeLadder.IsDue(null, D("2025-07-01"), D("2025-07-01"), settings));
    Assert.False(NoticeLadder.IsDue(NoticeStage.First, D("2025-07-01"), D("2025-07-10"), settings));
    Assert.True(NoticeLadder.IsDue(NoticeStage.First, D("2025-07-01"), D("2025-07-11"), settings));
    // a final notice made by hand: the second is never prepared, the show cause comes on its day
    Assert.False(NoticeLadder.IsDue(NoticeStage.Final, D("2025-07-01"), D("2025-07-25"), settings));
    Assert.True(NoticeLadder.IsDue(NoticeStage.Final, D("2025-07-01"), D("2025-07-31"), settings));
    Assert.False(NoticeLadder.IsDue(NoticeStage.ShowCause, D("2025-07-01"), D("2026-06-01"), settings));
  }

  [Fact]
  public void A_notice_table_needs_a_row_for_every_head_a_notice_can_carry()
  {
    var error = Assert.Throws<DomainException>(() => NoticeTableLayout.Create(NoticeTableLayoutId.New(),
      new TableLayoutInput([new("Property Tax", [HeadCodes.PropertyTax])], [], false, "r"), HeadNames, [HeadCodes.PropertyTax, HeadCodes.LeaseMoney], D("2025-07-01")));
    Assert.Contains("Lease money / ground rent has none", error.Message);
  }

  // ---- surcharges ----

  [Fact]
  public void An_in_year_surcharge_is_applied_once_and_defaulters_are_those_still_owing_earlier_dues()
  {
    var file = File();
    var demand = Issued(file, 2025);
    var arrears = demand.AddManualArrears(Lease, 2024, 10000, "Arrears 2024-25", "Old register", Officer, Now);
    var rule = IntraYearRule();
    var terms = new SurchargeTerms(2025, null, TargetSet.DefaultersOnly, false, PenaltyRateType.Percent, 5, null);

    var first = SurchargeRules.Candidate(demand, file, rule, terms, Settings, D("2025-10-01"))!;
    Assert.True(first.Eligible);
    Assert.Equal(10000m, first.Base);
    demand.AddPenalty(rule, null, first.Base, first.Amount, 5, D("2025-10-01"));

    var again = SurchargeRules.Candidate(demand, file, rule, terms, Settings, D("2025-11-01"))!;
    Assert.False(again.Eligible);
    Assert.Equal("surcharged on 01/10/2025", again.Reason);

    // the earlier dues paid off: no longer a defaulter
    var other = Issued(File(), 2025);
    other.AddManualArrears(Lease, 2024, 3000, "Arrears", "Old register", Officer, Now);
    other.Settle(3000, NothingPaid, Surcharge.Id, D("2025-08-01"));
    Assert.Null(SurchargeRules.Candidate(other, file, rule, terms, Settings, D("2025-10-01")));
    Assert.Equal(0m, other.ArrearsOwed);
    Assert.True(arrears.Outstanding > 0);
  }

  // ---- credits, statuses, reversals ----

  [Fact]
  public void A_correction_below_what_was_paid_leaves_a_credit_and_the_status_follows_the_balance()
  {
    var demand = Issued(File(), 2025);
    var paid = new Dictionary<Guid, decimal>();
    foreach (var s in demand.Settle(demand.Balance, NothingPaid, Surcharge.Id, D("2025-08-01")))
      paid[s.ItemId] = s.Amount;
    Assert.Equal(DemandStatus.Paid, demand.Status);

    demand.Line(PropertyTax.Id).SetManual(10000);
    demand.AfterChange(D("2025-09-01"));
    Assert.Equal(4500m, demand.Excess);
    Assert.Equal(DemandStatus.Paid, demand.Status);

    // the credit moves on: the demand still has nothing to pay
    demand.ReleasePaid(4500, D("2025-09-02"));
    Assert.Equal(0m, demand.Excess);
    Assert.Equal(0m, demand.Balance);

    // a payment taken back: it owes again
    demand.ReleasePaid(2000, D("2025-09-03"));
    Assert.Equal(2000m, demand.Balance);
    Assert.Equal(DemandStatus.PartPaid, demand.Status);
    Assert.Null(demand.PaidOn);
  }

  [Fact]
  public void A_defaulted_demand_corrected_to_nothing_is_paid_and_one_owing_again_is_defaulted_again()
  {
    var demand = Issued(File(), 2025);
    demand.ApplyYearEnd(YearEndRule(), D("2026-07-01"));
    Assert.Equal(DemandStatus.Defaulted, demand.Status);
    demand.Settle(demand.Balance, NothingPaid, Surcharge.Id, D("2026-07-05"));
    Assert.Equal(DemandStatus.Paid, demand.Status);
    demand.ReleasePaid(1000, D("2026-07-06"));
    Assert.Equal(DemandStatus.Defaulted, demand.Status);
  }

  [Fact]
  public void A_payment_on_a_carried_year_that_is_taken_back_is_billed_again_on_the_demand_holding_the_balance()
  {
    var file = File();
    var last = Issued(file, 2025);
    var paid = new Dictionary<Guid, decimal>();
    foreach (var s in last.Settle(2000, NothingPaid, Surcharge.Id, D("2025-09-01")))
      paid[s.ItemId] = s.Amount;
    var next = Demand.Draft(DemandId.New(), "DMD-2026", null, file, file.CurrentBasis!, file.Addressee(D("2026-07-01"))!, 2026, Assess(file, 2026).Lines, 0);
    next.Issue(D("2026-07-01"), D("2026-08-31"), last.CarryInto(next, paid, Surcharge.Id, Officer, Now, D("2026-07-01")));
    var before = next.Balance;

    last.ReleasePaidIntoCarried(2000);
    var row = next.BillAgain(PropertyTax.Id, 2025, 2000, last.Id, Officer, Now);
    Assert.Equal(22500m, last.CarriedAmount);
    Assert.Equal(before + 2000m, next.Balance);
    Assert.Equal(2025, row.FiscalYear);
  }

  [Fact]
  public void A_payment_needs_its_receipt_number_or_bank_reference_and_has_no_made_up_reference()
  {
    var file = File();
    Assert.Contains("receipt number", Assert.Throws<DomainException>(() =>
      Payment.Receive(PaymentId.New(), file, "Asif", PaymentChannel.Cash, 100, null, null, null, Now, Officer, Now)).Message);
    var cash = Payment.Receive(PaymentId.New(), file, "Asif Jadoon", PaymentChannel.Cash, 100, null, "MR-00001", null, Now, Officer, Now);
    Assert.Null(cash.TransactionRef);
    Assert.Equal("Asif Jadoon", cash.PayerName);

    cash.Reverse("cheque bounced", Officer, Now);
    Assert.Equal(PaymentStatus.Reversed, cash.Status);
    Assert.Contains("already reversed", Assert.Throws<DomainException>(() => cash.Reverse("again", Officer, Now)).Message);
  }

  // ---- fees and clearances ----

  [Fact]
  public void A_fee_is_paid_by_its_receipt_and_due_again_when_the_receipt_is_reversed()
  {
    var fee = Fee.Charge(FeeId.New(), "FEE-00001", TransferFee, null, PartyId.New(), FeeSourceType.Transfer, null, "TRF-1", null, null, null, 5000, null);
    Assert.Equal(FeeStatus.Due, fee.Status);
    var receipt = Payment.ForFee(PaymentId.New(), fee, null, "Imran", PaymentChannel.Cash, null, "MR-00002", null, Now, Officer, Now);
    Assert.Equal(FeeStatus.Paid, fee.Status);
    Assert.Equal(5000m, receipt.Amount);
    Assert.Equal(fee.Id, receipt.Allocations.Single().FeeId);
    fee.Unpay();
    Assert.Equal(FeeStatus.Due, fee.Status);

    Assert.Contains("charged on notices", Assert.Throws<DomainException>(() =>
      Fee.Charge(FeeId.New(), "FEE-2", Lease, null, PartyId.New(), null, null, null, null, null, null, 100, null)).Message);
  }

  [Fact]
  public void A_clearance_needs_no_dues_and_no_fees_due_and_is_valid_exactly_its_days()
  {
    var file = File();
    Assert.Contains("fees due", Assert.Throws<DomainException>(() =>
      Clearance.Issue(ClearanceId.New(), "CLR-1", file, "Asif", ClearancePurpose.Transfer, 0, 2000, null, null, null, D("2025-10-01"), 30, Officer)).Message);
    var clearance = Clearance.Issue(ClearanceId.New(), "CLR-2", file, "Asif Jadoon", ClearancePurpose.Transfer, 0, 0, null, null, null, D("2025-10-01"), 30, Officer);
    Assert.Equal(D("2025-10-30"), clearance.ValidUntil);
    Assert.Equal(ClearanceStatus.Issued, clearance.StatusOn(D("2025-10-30")));
    Assert.Equal(ClearanceStatus.Expired, clearance.StatusOn(D("2025-10-31")));
    clearance.Consume(D("2025-10-15"), Now, Officer, ClearancePurpose.Transfer);
    Assert.Equal(Officer, clearance.ConsumedBy);
  }

  // ---- opening arrears ----

  [Fact]
  public void An_opening_due_is_paid_before_the_first_demand_and_corrected_never_below_what_was_paid()
  {
    var file = File();
    var row = Arrears.Imported(ArrearsId.New(), file.Id, Lease, 2022, 9000, "Old register", null, Officer, Now);
    Assert.True(row.IsUnbilledOpening);
    row.Settle(4000);
    Assert.Contains("already paid", Assert.Throws<DomainException>(() => row.CorrectOpening(Lease, 2022, 3000, null, "typo", Officer, Now)).Message);
    row.CorrectOpening(Lease, 2022, 6000, null, "register checked", Officer, Now);
    Assert.Equal(2000m, row.Outstanding);

    // billed on the first demand, what was paid on it counts there too
    var demand = Demand.Draft(DemandId.New(), "DMD-1", null, file, file.CurrentBasis!, file.Addressee(D("2025-07-01"))!, 2025, Assess(file, 2025).Lines, 0);
    demand.Issue(D("2025-07-01"), D("2025-08-31"), [row]);
    Assert.Equal(4000m, demand.PaidAmount);
    Assert.Equal(22500m + 2000m, demand.Balance);
    Assert.False(row.IsUnbilledOpening);
  }

  // ---- things dated in the future by mistake ----

  [Fact]
  public void An_override_or_hold_dated_ahead_by_mistake_is_taken_back_and_the_earlier_override_runs_on()
  {
    var file = File();
    file.AddOverride(PropertyTax, OverrideMode.Minimum, null, 20000, false, "Board", null, D("2025-07-01"), Officer, Now);
    var later = file.AddOverride(PropertyTax, OverrideMode.Minimum, null, 30000, false, "typo", null, D("2027-07-01"), Officer, Now);
    Assert.Equal(D("2027-06-30"), file.Overrides.First().EffectiveTo);
    Assert.Contains("in force since", Assert.Throws<DomainException>(() => file.VoidOverride(file.Overrides.First().Id, D("2026-01-01"))).Message);
    file.VoidOverride(later.Id, D("2026-01-01"));
    Assert.Single(file.Overrides);
    Assert.Null(file.Overrides.Single().EffectiveTo);

    var hold = file.AddHold("Writ", null, null, D("2027-01-01"), true, true);
    file.VoidHold(hold.Id, D("2026-01-01"));
    Assert.Empty(file.Holds);
  }

  [Fact]
  public void A_setting_or_rule_version_from_a_later_date_is_taken_back_before_it_is_in_force()
  {
    var now = TaxSetting.Create(TaxSettingId.New(), SettingKeys.AnnualIncreasePct, "10", D("2025-07-01"));
    var history = new List<TaxSetting> { now };
    var future = TaxSetting.Change(now, SettingKeys.AnnualIncreasePct, "50", D("2030-07-01"), null, history)!;
    history.Add(future);
    Assert.Equal(D("2030-06-30"), now.EffectiveTo);
    TaxSetting.Void(future, history, D("2026-01-01"));
    Assert.Null(now.EffectiveTo);

    var rule = IntraYearRule();
    var next = rule.Revise(new PenaltyRuleInput(null, PenaltyRateType.Percent, 3, TargetSet.DefaultersOnly, true, true, 3, PeriodUnit.Months, PeriodAnchor.FyStart, null),
      D("2026-07-01"), [rule])!;
    Assert.Equal(D("2026-06-30"), rule.EffectiveTo);
    Assert.Equal(3m, next.Rate);
    Assert.Equal(2m, rule.Rate);
    Assert.Contains("in force since", Assert.Throws<DomainException>(() => PenaltyRule.Void(rule, [rule, next], D("2026-01-01"))).Message);
    PenaltyRule.Void(next, [rule, next], D("2026-01-01"));
    Assert.Null(rule.EffectiveTo);
  }

  [Fact]
  public void A_town_assignment_dated_ahead_by_mistake_is_taken_back_and_the_town_stays_where_it_was()
  {
    var first = TaxCircle.Create(TaxCircleId.New(), "C-I", new CircleDetails("Circle I", null, null, null, null, 1));
    var second = TaxCircle.Create(TaxCircleId.New(), "C-II", new CircleDetails("Circle II", null, null, null, null, 2));
    var history = new List<TaxCircleTown> { first.AssignTown(TaxCircleTownId.New(), Town, "NATHIAGALI", "NG", D("2025-07-01"), []) };
    var move = second.AssignTown(TaxCircleTownId.New(), Town, "NATHIAGALI", "NG", D("2027-07-01"), history);
    history.Add(move);
    Assert.Equal(D("2027-06-30"), history[0].EffectiveTo);
    second.VoidTown(move.Id, D("2026-01-01"), history);
    Assert.Null(history[0].EffectiveTo);
    Assert.Empty(second.Towns);
  }

  // ---- the calculator ----

  [Fact]
  public void A_head_added_after_the_tariff_is_noted_not_charged_and_round_up_counts_part_units_for_a_rate_head()
  {
    var signboard = TaxHead.Regular(TaxHeadId.New(), "SIGNBOARD_FEE", new RegularHeadInput("Signboard fee", CalcMethod.Rate, AppliesTo.Both, ChargedFrom.Always,
      null, null, null, false, true, 5, true, null, null, null));
    var heads = Heads.Append(signboard).ToList();
    var a = Assess(File(), 2025, heads: heads);
    Assert.DoesNotContain(a.Lines, l => l.HeadId == signboard.Id);
    Assert.Contains(a.Notes, n => n.Contains("Signboard fee") && n.Contains("not charged"));

    var set = global::RateSet.Create(RateSetId.New(), "Copy", "Board", null, D("2025-07-01"));
    set.CopyRatesFrom(RateSet());
    set.SetHeadRates(signboard, TaxCategory.Res, null, new HeadRateInput(RateBasis.Kanal, null, null, null, true, 1000, null, null, null));
    var rated = Assess(File(), 2025, set, heads: heads);
    // 400 sq yd = 0.88 kanal, charged as one
    Assert.Equal(1000m, Line(rated, signboard));
  }

  [Fact]
  public void A_rate_set_needs_a_head_s_rates_for_every_town_in_a_circle_or_for_all_towns()
  {
    var signboard = TaxHead.Regular(TaxHeadId.New(), "SIGNBOARD_FEE", new RegularHeadInput("Signboard fee", CalcMethod.Fixed, AppliesTo.Res, ChargedFrom.Always,
      null, null, null, false, true, 5, true, null, null, null));
    var other = Guid.NewGuid();
    var set = global::RateSet.Create(RateSetId.New(), "Copy", "Board", null, D("2025-07-01"));
    set.CopyRatesFrom(RateSet());
    foreach (var category in new[] { TaxCategory.Res, TaxCategory.Com })
    foreach (var component in new[] { LtfComponent.Land, LtfComponent.Covered })
      set.SetLtfFactors(other, category, component, [new(null, 5)]);
    set.SetHeadRates(signboard, TaxCategory.Res, Town, new HeadRateInput(RateBasis.Fixed, null, null, null, false, null, 1000, null, null));
    var error = Assert.Throws<DomainException>(() => set.Approve(Officer, Now, Heads.Append(signboard).ToList(), [(Town, "NATHIAGALI"), (other, "DUNGA")], []));
    Assert.Contains("DUNGA", error.Message);
  }

  private static Notice PrepareNotice(Demand? demand = null, TaxFile? file = null)
  {
    file ??= File();
    demand ??= Issued(file, 2025);
    var template = NoticeTemplate.Create(NoticeTemplateId.New(), NoticeStage.First, "First", "1st NOTICE", "Pay {amount} within {days}.", null, D("2025-07-01"));
    var letterhead = NoticeLetterhead.Create(NoticeLetterheadId.New(), new LetterheadInput("GOVT", "GDA", null, [], [], null, null, true, "ref"), null, null, D("2025-07-01"));
    var variables = new NoticeVariables { PayerName = "Asif Jadoon", RefId = "NA/R/01", Town = "NG", AddressLine = "Asif Jadoon, Chairlift Road", Days = 10, Paragraph = template.ParagraphText };
    var table = NoticeTable.Build(demand, demand.Items(NothingPaid, Surcharge.Id), HeadsById, Layout());
    return Notice.Prepare(NoticeId.New(), "GDA-TAX-2025-0001", demand, file, NoticeStage.First, NoticeOrigin.Cycle, D("2025-07-01"),
      D("2025-07-15"), template, letterhead, Layout(), PayVia.Both, variables, table, null, true);
  }
}
