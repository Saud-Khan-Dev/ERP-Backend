using static Fixture;

/// Demands, payments, carrying a balance forward, penalties, notices and the rules of the setup.
public class DemandAndNoticeTests
{
  [Fact]
  public void A_payment_settles_earlier_years_before_this_year()
  {
    var file = File();
    var demand = Issued(file, 2025);
    demand.AddManualArrears(Misc, 2023, 3000, "Unpaid water 2023-24", "Old register", Officer, Now);
    Assert.Equal(14500m + 8000m + 3000m, demand.Balance);

    var settled = demand.Settle(4000, NothingPaid, Surcharge.Id, D("2025-08-01"));
    Assert.Equal(3000m, settled[0].Amount);
    Assert.Equal(DemandItemKind.Arrears, settled[0].Kind);
    Assert.Equal(1000m, settled[1].Amount);
    Assert.Equal(DemandStatus.PartPaid, demand.Status);
    Assert.Equal(21500m, demand.Balance);
  }

  [Fact]
  public void A_demand_paid_in_full_is_paid_on_that_day()
  {
    var demand = Issued(File(), 2025);
    demand.Settle(demand.Balance, NothingPaid, Surcharge.Id, D("2025-08-15"));
    Assert.Equal(DemandStatus.Paid, demand.Status);
    Assert.Equal(D("2025-08-15"), demand.PaidOn);
    Assert.False(demand.IsOpen);
  }

  [Fact]
  public void The_unpaid_balance_moves_onto_the_next_year_by_year_and_head()
  {
    var file = File();
    var last = Issued(file, 2025);
    var paid = new Dictionary<Guid, decimal>();
    foreach (var s in last.Settle(2000, NothingPaid, Surcharge.Id, D("2025-09-01")))
      paid[s.ItemId] = s.Amount;

    var next = Demand.Draft(DemandId.New(), "DMD-2026", null, file, file.CurrentBasis!, file.Addressee(D("2026-07-01"))!, 2026, Assess(file, 2026).Lines, 0);
    var rows = last.CarryInto(next, paid, Surcharge.Id, Officer, Now);

    Assert.Equal(DemandStatus.Carried, last.Status);
    Assert.Equal(0m, last.Balance);
    Assert.Equal(20500m, last.CarriedAmount);
    Assert.Equal(next.Id, last.CarriedToDemandId);
    Assert.Equal(2, rows.Count);
    Assert.All(rows, r => Assert.Equal(2025, r.FiscalYear));
    Assert.Equal(12500m, rows.Single(r => r.HeadId == PropertyTax.Id).OriginalAmount);
    Assert.Equal(8000m, rows.Single(r => r.HeadId == Conservancy.Id).OriginalAmount);
    Assert.Equal(20500m, next.ArrearsBf);
  }

  [Fact]
  public void The_year_end_surcharge_is_ten_percent_of_the_outstanding_once_and_marks_the_demand_defaulted()
  {
    var demand = Issued(File(), 2025);
    Assert.Null(demand.ApplyYearEnd(YearEndRule(), D("2026-06-30")));

    var penalty = demand.ApplyYearEnd(YearEndRule(), D("2026-07-01"));
    Assert.NotNull(penalty);
    Assert.Equal(2250m, penalty!.Amount);
    Assert.Equal(DemandStatus.Defaulted, demand.Status);
    Assert.Equal(24750m, demand.Balance);
    Assert.Null(demand.ApplyYearEnd(YearEndRule(), D("2026-07-02")));
  }

  [Fact]
  public void An_in_year_surcharge_targets_defaulters_only_after_its_period()
  {
    var file = File();
    var demand = Issued(file, 2025);
    var terms = new SurchargeTerms(2025, null, TargetSet.DefaultersOnly, true, PenaltyRateType.Percent, 5, null);
    Assert.Null(SurchargeRules.Candidate(demand, file, IntraYearRule(), terms, Settings, D("2025-10-01")));

    demand.AddManualArrears(Misc, 2024, 10000, "Arrears 2024-25", "Old register", Officer, Now);
    var early = SurchargeRules.Candidate(demand, file, IntraYearRule(), terms, Settings, D("2025-09-30"))!;
    Assert.False(early.Eligible);
    Assert.Contains("period runs until 2025-10-01", early.Reason);

    var due = SurchargeRules.Candidate(demand, file, IntraYearRule(), terms, Settings, D("2025-10-01"))!;
    Assert.True(due.Eligible);
    Assert.Equal(Rupees.Round(32500m * 0.05m), due.Amount);
  }

  [Fact]
  public void Earlier_dues_cannot_be_dated_after_the_demand_year_and_other_charges_need_a_description()
  {
    var demand = Issued(File(), 2025);
    Assert.Contains("after the demand's year", Assert.Throws<DomainException>(() => demand.AddManualArrears(Misc, 2026, 100, "x", "r", Officer, Now)).Message);
    Assert.Contains("Describe", Assert.Throws<DomainException>(() => demand.AddManualArrears(Misc, 2024, 100, null, "r", Officer, Now)).Message);
  }

  [Fact]
  public void The_notice_table_adds_up_to_the_balance_and_puts_unplaced_heads_on_the_other_row()
  {
    var demand = Issued(File(), 2025);
    demand.AddManualArrears(Misc, 2024, 3000, "Old dues", "Old register", Officer, Now);
    var paid = new Dictionary<Guid, decimal>();
    foreach (var s in demand.Settle(5000, NothingPaid, Surcharge.Id, D("2025-09-01")))
      if (s.Kind != DemandItemKind.Arrears)
        paid[s.ItemId] = s.Amount;

    var table = NoticeTable.Build(demand, demand.Items(paid, Surcharge.Id), HeadCodesById, Layout());
    Assert.Equal(demand.Balance, table.Totals.Payable);
    Assert.Equal(5000m, table.Totals.Received);
    var other = table.Rows.Single(r => r.Label == "Other charges / earlier dues");
    Assert.Equal(3000m, other.Arrears);
    Assert.Equal(0m, other.Payable);
  }

  [Fact]
  public void Rows_that_are_all_zero_can_be_left_out_of_the_notice_table()
  {
    var demand = Issued(File(), 2025);
    var full = NoticeTable.Build(demand, demand.Items(NothingPaid, Surcharge.Id), HeadCodesById, Layout());
    var trimmed = NoticeTable.Build(demand, demand.Items(NothingPaid, Surcharge.Id), HeadCodesById, Layout(hideZero: true));
    Assert.Equal(5, full.Rows.Count);
    Assert.Equal(2, trimmed.Rows.Count);
    Assert.Equal(full.Totals, trimmed.Totals);
  }

  [Fact]
  public void A_head_is_in_at_most_one_row_and_the_table_needs_an_amount_column()
  {
    var known = Heads.Select(h => h.Code).ToHashSet();
    var twice = Assert.Throws<DomainException>(() => NoticeTableLayout.Create(NoticeTableLayoutId.New(), new TableLayoutInput(
      [new("A", [HeadCodes.PropertyTax]), new("B", [HeadCodes.PropertyTax])], [], "Other", false, "r"), known, D("2025-07-01")));
    Assert.Contains("in two rows", twice.Message);

    var noAmounts = Assert.Throws<DomainException>(() => NoticeTableLayout.Create(NoticeTableLayoutId.New(), new TableLayoutInput(
      [new("A", [HeadCodes.PropertyTax])], NoticeTableLayout.AmountColumns.Select(k => new TableColumnInput(k, null, false)).ToList(), "Other", false, "r"), known, D("2025-07-01")));
    Assert.Contains("amount column", noAmounts.Message);
  }

  [Fact]
  public void A_notice_prints_its_paragraph_its_table_or_both()
  {
    var notice = Prepare();
    Assert.Contains("paragraph, the table, or both", Assert.Throws<DomainException>(() => notice.SetPrinting(false, false, null)).Message);
    notice.SetPrinting(false, true, null);
    Assert.False(notice.ShowsParagraph);
  }

  [Fact]
  public void Changing_a_printed_field_before_issue_needs_a_reason_and_is_recorded()
  {
    var notice = Prepare();
    Assert.Contains("reason", Assert.Throws<DomainException>(() => notice.Change(new NoticeChanges(PlotNo: "99-A"), [], Officer, Now)).Message);
    notice.Change(new NoticeChanges(PlotNo: "99-A", Reason: "from the allotment file"), [], Officer, Now);
    Assert.Equal("99-A", notice.Variables.PlotNo);
    Assert.Equal("plotNo", notice.EditedFields!.Single().Field);
  }

  [Fact]
  public void An_issued_notice_freezes_its_amount_and_cannot_change()
  {
    var file = File();
    var demand = Issued(file, 2025);
    var notice = Prepare(demand, file);
    var table = NoticeTable.Build(demand, demand.Items(NothingPaid, Surcharge.Id), HeadCodesById, Layout());
    notice.Confirm(demand, table, "QR", new SigningOfficer(null, null, "Mr Tariq Khan"), Officer, Now);
    Assert.Equal(NoticeStatus.Issued, notice.Status);
    Assert.Equal(demand.Balance, notice.AmountWithinDue);
    Assert.Equal(DemandStage.First, demand.Stage);
    Assert.Throws<DomainException>(() => notice.Change(new NoticeChanges(PlotNo: "1", Reason: "x"), [], Officer, Now));
    Assert.Throws<DomainException>(() => notice.Serve(null, D("2025-06-30"), ServiceMode.ByHand, D("2025-08-01")));
    notice.Serve(null, D("2025-07-03"), ServiceMode.ByHand, D("2025-08-01"));
    Assert.Equal(ServiceMode.ByHand, notice.ServiceMode);
  }

  [Fact]
  public void Notice_numbers_never_repeat_and_follow_the_Sections_format()
  {
    var codes = Enumerable.Range(1, 20000).Select(n => Numbers.NoticeNo(2026, n)).ToList();
    Assert.Equal(codes.Count, codes.Distinct().Count());
    Assert.Matches(@"^GDA-TAX-2026-[0-9A-Z]{4}$", codes[0]);
    Assert.Equal("TAX/C-I/2026-27/00001", Numbers.EndstNo("C-I", 2026, 1));
    Assert.Equal("6011 0000 0022", Numbers.FormatPsid(Numbers.Psid("6011", 22)));
  }

  [Fact]
  public void The_payment_qr_carries_the_amount_and_the_reference_with_a_valid_crc()
  {
    Assert.Equal("29B1", PaymentQr.Crc16("123456789"));
    var payload = PaymentQr.Payload(Settings, "601100000022", 436275, "GDA-TAX-2026-3SN6");
    Assert.Contains("5409436275.00", payload);
    Assert.Contains("0112601100000022", payload);
    Assert.Equal(PaymentQr.Crc16(payload[..^4]), payload[^4..]);
  }

  [Fact]
  public void Notice_periods_count_working_days_and_skip_holidays()
  {
    // Friday 1 Aug 2025 + 10 working days, with 14 August a holiday
    var calendar = new WorkCalendar(true, new HashSet<DateOnly> { D("2025-08-14") });
    Assert.Equal(D("2025-08-18"), calendar.AddDays(D("2025-08-01"), 10));
    Assert.Equal(D("2025-08-11"), new WorkCalendar(false, new HashSet<DateOnly>()).AddDays(D("2025-08-01"), 10));
  }

  [Fact]
  public void Band_limits_must_rise_and_only_the_last_band_is_open()
  {
    var set = global::RateSet.Create(RateSetId.New(), "Draft", "Board", null, D("2026-07-01"));
    Assert.Contains("must rise", Assert.Throws<DomainException>(() => set.SetLtfFactors(Town, TaxCategory.Res, LtfComponent.Land, [new(500, 5), new(400, 6)])).Message);
    Assert.Contains("only the last", Assert.Throws<DomainException>(() => set.SetLtfFactors(Town, TaxCategory.Res, LtfComponent.Land, [new(null, 5), new(400, 6)])).Message);
  }

  [Fact]
  public void An_approved_rate_set_cannot_change_and_a_set_cannot_be_approved_incomplete()
  {
    var approved = RateSet();
    Assert.Contains("approved", Assert.Throws<DomainException>(() => approved.SetLtfFactors(Town, TaxCategory.Res, LtfComponent.Land, [new(null, 5)])).Message);

    var draft = global::RateSet.Create(RateSetId.New(), "Draft", "Board", null, D("2026-07-01"));
    Assert.Contains("not complete", Assert.Throws<DomainException>(() => draft.Approve(Officer, Now, Heads, [(Town, "NATHIAGALI")], [approved])).Message);

    draft.CopyRatesFrom(approved);
    draft.Approve(Officer, Now, Heads, [(Town, "NATHIAGALI")], [approved]);
    Assert.Equal(RateSetStatus.Superseded, approved.Status);
    Assert.Equal(D("2026-06-30"), approved.EffectiveTo);
  }

  [Fact]
  public void The_notice_days_must_rise_from_second_to_show_cause()
  {
    var settings = TaxSettings.From(new Dictionary<string, string> { [SettingKeys.NoticeFinalDays] = "8" });
    Assert.Throws<DomainException>(settings.EnsureConsistent);
    Assert.Equal(10, Settings.ReplyDays(NoticeStage.First));
    Assert.Equal(new DateOnly(2025, 8, 31), Settings.DueDate(2025));
  }

  [Fact]
  public void A_tax_formula_accepts_only_its_variables()
  {
    Assert.Equal(4500m, TaxFormula.Parse("(a * ltf_land) + (B * LTF_COVERED)").Evaluate(new Dictionary<string, decimal> { ["A"] = 400, ["LTF_LAND"] = 5, ["B"] = 500, ["LTF_COVERED"] = 5 }));
    Assert.Contains("not a known value", Assert.Throws<DomainException>(() => TaxFormula.Parse("A * SALARY")).Message);
  }

  [Fact]
  public void An_override_needs_a_head_that_allows_it_and_one_override_per_start_date()
  {
    var file = File();
    Assert.Throws<DomainException>(() => file.AddOverride(Rent, OverrideMode.Minimum, null, 1000, true, "x", null, D("2025-07-01"), Officer, Now));
    file.AddOverride(PropertyTax, OverrideMode.Minimum, null, 1000, true, "x", null, D("2025-07-01"), Officer, Now);
    Assert.Throws<DomainException>(() => file.AddOverride(PropertyTax, OverrideMode.Minimum, null, 2000, true, "y", null, D("2025-07-01"), Officer, Now));
    file.AddOverride(PropertyTax, OverrideMode.Minimum, null, 2000, true, "y", null, D("2026-07-01"), Officer, Now);
    Assert.Equal(D("2026-06-30"), file.Overrides[0].EffectiveTo);
  }

  [Fact]
  public void A_sync_that_finds_the_same_facts_keeps_the_basis_and_a_transfer_changes_the_payers()
  {
    var snapshot = Snapshot();
    var file = File(snapshot: snapshot);
    var first = file.CurrentBasis!.Id;
    Assert.False(file.Sync(snapshot, D("2026-01-01"), Now));
    Assert.Equal(first, file.CurrentBasis!.Id);

    var buyer = new PayerSnapshot(Guid.NewGuid(), "OWN-00002", PayerRole.Owner, PayerSource.Ownership, Guid.NewGuid(), 100, "New Owner", null, null, null, null, D("2026-02-01"));
    Assert.True(file.Sync(snapshot with { LandAreaSqft = 5400, Payers = [buyer] }, D("2026-02-01"), Now));
    Assert.NotEqual(first, file.CurrentBasis!.Id);
    Assert.Equal("New Owner", file.Addressee(D("2026-02-01"))!.Name);
    Assert.Equal(D("2026-01-31"), file.Payers.Single(p => p.Name == "Asif Jadoon").EffectiveTo);
  }

  [Theory]
  [InlineData("9999900009029", "99999-0000902-9")]
  [InlineData(" 99999-0000902-9 ", "99999-0000902-9")]
  [InlineData("1234567-8", "1234567-8")]
  [InlineData("NTN 1234567", "NTN 1234567")]
  public void A_party_CNIC_is_kept_the_way_Property_keeps_it_and_an_NTN_as_typed(string typed, string kept)
  {
    var party = Party.Create(PartyId.New(), PartyType.Applicant, "Imran Khattak", typed, null, null, null, null);
    Assert.Equal(kept, party.CnicNtn);
  }

  private static Notice Prepare(Demand? demand = null, TaxFile? file = null)
  {
    file ??= File();
    demand ??= Issued(file, 2025);
    var template = NoticeTemplate.Create(NoticeTemplateId.New(), NoticeStage.First, "First", "1st NOTICE", "Pay {amount} within {days}.", null, D("2025-07-01"));
    var letterhead = NoticeLetterhead.Create(NoticeLetterheadId.New(), new LetterheadInput("GOVT", "GDA", null, [], [], null, null, true, "ref"), null, null, D("2025-07-01"));
    var variables = new NoticeVariables { PayerName = "Asif Jadoon", RefId = "NA/R/01", Town = "NG", AddressLine = "Asif Jadoon, Chairlift Road", Days = 10, Paragraph = template.ParagraphText };
    var table = NoticeTable.Build(demand, demand.Items(NothingPaid, Surcharge.Id), HeadCodesById, Layout());
    return Notice.Prepare(NoticeId.New(), "GDA-TAX-2025-0001", "TAX/C-I/2025-26/00001", demand, file, NoticeStage.First, NoticeOrigin.Cycle, D("2025-07-01"),
      D("2025-07-15"), template, letterhead, Layout(), PayVia.Both, variables, table, null, true);
  }
}
