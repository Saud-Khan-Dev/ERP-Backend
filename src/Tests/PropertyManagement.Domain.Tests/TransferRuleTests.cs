using static Fixture;
using static OwnershipFixture;

/// Approve = complete, double commitment, no-op transfers, dates, partial sellers and HoldingSince, disputed and
/// charged shares, reject / cancel, deactivated property.
public class TransferRuleTests
{
  // ---- approving IS the mutation ----

  [Fact]
  public void Approving_a_transfer_registers_it_in_the_same_step()
  {
    var (property, _) = NewPlot();
    PropertyOwner b = Owner("B"), d = Owner("D");
    var ownB = Own(property, b, 100);
    var record = Record(property, new[] { ownB }, new[] { b, d });
    var transfer = Initiate(record, "2026-10-01", "TRF-00001", Gives(b, 100), Gets(d, 100));

    var change = transfer.Approve(Director, D("2026-10-12"), "ORDER-9", null, "clerk1", record, Owned);

    Assert.Equal(TransferStatus.Completed, transfer.TransferStatus);
    Assert.Equal("Director Estate", transfer.ApprovedBy);
    Assert.Equal(D("2026-10-12"), transfer.ApprovalDate);
    Assert.Equal("ORDER-9", transfer.ApprovalOrderRef);
    Assert.Equal(Today, transfer.CompletedOn);
    Assert.Equal("clerk1", transfer.CompletedBy);

    // the previous owner no longer holds anything: their row is closed on the transfer date
    Assert.Equal(OwnershipStatus.Ended, ownB.OwnershipStatus);
    Assert.Equal(D("2026-10-01"), ownB.EffectiveTo);
    var opened = Assert.Single(change.Opened);
    Assert.Equal(d.Id, opened.OwnerId);
    Assert.Equal(100, opened.OwnershipSharePct);
    Assert.Equal(D("2026-10-01"), opened.EffectiveFrom);
  }

  [Fact]
  public void A_completed_transfer_cannot_be_approved_again_or_cancelled()
  {
    var (property, _) = NewPlot();
    PropertyOwner b = Owner("B"), d = Owner("D");
    var record = Record(property, new[] { Own(property, b, 100) }, new[] { b, d });
    var transfer = Initiate(record, "2026-10-01", "TRF-00001", Gives(b, 100), Gets(d, 100));
    Approve(transfer, record);

    Assert.Contains("completed", Message(() => Approve(transfer, record)));
    Assert.Contains("record a new transfer", Message(() => transfer.Cancel(property, "typo", "clerk1", Today)));
  }

  [Fact]
  public void A_legacy_approved_transfer_is_completed_and_a_waiting_one_is_not()
  {
    var (property, _) = NewPlot();
    PropertyOwner b = Owner("B"), d = Owner("D");
    var ownB = Own(property, b, 100);
    var record = Record(property, new[] { ownB }, new[] { b, d });
    var transfer = Initiate(record, "2026-10-01", "TRF-00001", Gives(b, 100), Gets(d, 100));

    Assert.Contains("waiting for approval", Message(() => transfer.Complete("clerk1", null, record, Owned)));

    MakeLegacyApproved(transfer);
    Assert.Contains("complete it", Message(() => Approve(transfer, record)));

    var change = transfer.Complete("clerk1", "ORDER-7", record, Owned);

    Assert.Equal(TransferStatus.Completed, transfer.TransferStatus);
    Assert.Equal(Today, transfer.CompletedOn);
    Assert.Equal("clerk1", transfer.CompletedBy);
    Assert.Equal("ORDER-7", transfer.ApprovalOrderRef);
    Assert.Equal(OwnershipStatus.Ended, ownB.OwnershipStatus);
    Assert.Single(change.Opened);
  }

  // ---- double commitment ----

  [Fact]
  public void A_share_already_being_transferred_cannot_be_given_again()
  {
    var (property, _) = NewPlot();
    PropertyOwner b = Owner("B"), c = Owner("C"), d = Owner("D");
    var rows = new[] { Own(property, b, 60), Own(property, c, 40) };
    var first = Initiate(Record(property, rows, new[] { b, c, d }), "2026-10-01", "TRF-00041", Gives(b, 40), Gets(d, 40));

    var record = Record(property, rows, new[] { b, c, d }, pending: new[] { first });

    var message = Message(() => Initiate(record, "2026-10-02", "TRF-00042", Gives(b, 30), Gets(c, 30)));
    Assert.Contains("TRF-00041", message);
    Assert.Contains("cancel or approve that first", message);

    // what is left over can still be given
    Initiate(record, "2026-10-02", "TRF-00043", Gives(b, 20), Gets(c, 20));
  }

  [Fact]
  public void Approval_checks_the_other_waiting_transfers_too()
  {
    var (property, _) = NewPlot();
    PropertyOwner b = Owner("B"), c = Owner("C"), d = Owner("D");
    var rows = new[] { Own(property, b, 60), Own(property, c, 40) };
    var owners = new[] { b, c, d };

    // both were initiated against an older record (e.g. before a correction); together they over-commit B
    var first = Initiate(Record(property, rows, owners), "2026-10-01", "TRF-00041", Gives(b, 40), Gets(d, 40));
    var second = Initiate(Record(property, rows, owners), "2026-10-01", "TRF-00042", Gives(b, 30), Gets(c, 30));

    var record = Record(property, rows, owners, pending: new[] { first, second });
    var message = Message(() => Approve(second, record));
    Assert.Contains("TRF-00041", message);
  }

  [Fact]
  public void A_transfer_that_no_longer_balances_is_refused_at_approval_without_being_cancelled()
  {
    var (property, _) = NewPlot();
    PropertyOwner b = Owner("B"), d = Owner("D");
    var owners = new[] { b, d };
    var transfer = Initiate(Record(property, new[] { Own(property, b, 60) }, owners), "2026-10-01", "TRF-00042", Gives(b, 40), Gets(d, 40));

    // by approval time B holds only 20%
    var record = Record(property, new[] { Own(property, b, 20, "2020-01-01") }, owners);

    var message = Message(() => Approve(transfer, record));
    Assert.Contains("now holds 20%", message);
    Assert.Equal(TransferStatus.Initiated, transfer.TransferStatus);
  }

  // ---- no-op ----

  [Fact]
  public void A_transfer_that_moves_no_share_is_refused()
  {
    var (property, _) = NewPlot();
    PropertyOwner b = Owner("B"), c = Owner("C");
    var record = Record(property, new[] { Own(property, b, 60), Own(property, c, 40) }, new[] { b, c });

    Assert.Equal("This transfer moves no share.", Message(() => Initiate(record, "2026-10-01", "TRF-00001", Gives(b, 20), Gets(b, 20))));
    Assert.Equal("This transfer moves no share.",
      Message(() => Initiate(record, "2026-10-01", "TRF-00001", Gives(b, 30), Gives(c, 30), Gets(b, 30), Gets(c, 30))));
  }

  // ---- dates ----

  [Fact]
  public void A_future_dated_transfer_waits_until_its_date()
  {
    var (property, _) = NewPlot();
    PropertyOwner b = Owner("B"), d = Owner("D");
    var record = Record(property, new[] { Own(property, b, 100) }, new[] { b, d });

    var transfer = Initiate(record, "2026-10-20", "TRF-00007", Gives(b, 100), Gets(d, 100));

    Assert.Equal("TRF-00007 can be approved on or after 2026-10-20.", Message(() => Approve(transfer, record)));
    Approve(transfer, Record(property, record.Ownerships, new[] { b, d }, today: D("2026-10-20")), "2026-10-20");
    Assert.Equal(TransferStatus.Completed, transfer.TransferStatus);
  }

  [Fact]
  public void A_past_transfer_is_recorded_completed_in_one_step_but_never_future_dated()
  {
    var (property, _) = NewPlot();
    PropertyOwner b = Owner("B"), d = Owner("D");
    var ownB = Own(property, b, 100);
    var record = Record(property, new[] { ownB }, new[] { b, d });

    (PropertyTransfer, PropertyTransfer.OwnershipChange) Record_(string date) => PropertyTransfer.RecordCompleted(
      TransferId.New(), BusinessCode.Of("TRF-00003"), Sale, D(date), "MUT-7", null, null, null,
      new[] { Gives(b, 100), Gets(d, 100) }, Director, null, null, null, "clerk1", record, Owned);

    Assert.Contains("cannot be dated in the future", Message(() => Record_("2026-10-14")));

    var (transfer, change) = Record_("2021-05-01");
    Assert.Equal(TransferStatus.Completed, transfer.TransferStatus);
    Assert.Equal(D("2021-05-01"), transfer.ApprovalDate);
    Assert.Equal(Today, transfer.CompletedOn);
    Assert.Equal(D("2021-05-01"), ownB.EffectiveTo);
    Assert.Single(change.Opened);
  }

  [Fact]
  public void The_approval_date_lies_between_the_transfer_date_and_today()
  {
    var (property, _) = NewPlot();
    PropertyOwner b = Owner("B"), d = Owner("D");
    var record = Record(property, new[] { Own(property, b, 100) }, new[] { b, d });
    var transfer = Initiate(record, "2026-10-01", "TRF-00001", Gives(b, 100), Gets(d, 100));

    Assert.Contains("cannot be in the future", Message(() => Approve(transfer, record, "2026-10-14")));
    Assert.Contains("before its transfer date", Message(() => Approve(transfer, record, "2026-09-30")));
  }

  [Fact]
  public void A_transfer_dated_before_the_giver_holds_the_share_is_refused_at_initiation()
  {
    var (property, _) = NewPlot();
    PropertyOwner c = Owner("C"), d = Owner("D");
    var record = Record(property, new[] { Own(property, c, 100, "2026-10-01") }, new[] { c, d });

    Assert.Equal("C holds this share only since 2026-10-01; date the transfer after that.",
      Message(() => Initiate(record, "2026-09-15", "TRF-00001", Gives(c, 50), Gets(d, 50))));

    // the same day would put two entries for C on one date
    Assert.Contains("only since 2026-10-01", Message(() => Initiate(record, "2026-10-01", "TRF-00001", Gives(c, 50), Gets(d, 50))));
  }

  [Fact]
  public void A_receiver_with_a_later_entry_on_the_property_is_refused()
  {
    var (property, _) = NewPlot();
    PropertyOwner b = Owner("B"), d = Owner("D");
    var oldD = Own(property, d, 10, "2019-01-01");
    oldD.End(D("2026-06-01"));
    var record = Record(property, new[] { Own(property, b, 100), oldD }, new[] { b, d });

    Assert.Contains("runs until 2026-06-01", Message(() => Initiate(record, "2026-03-01", "TRF-00001", Gives(b, 50), Gets(d, 50))));
  }

  // ---- partial seller, HoldingSince ----

  [Fact]
  public void A_partial_seller_keeps_a_remainder_with_the_original_acquisition_details()
  {
    var (property, classification) = NewPlot();
    PropertyOwner b = Owner("B"), d = Owner("D");
    var allotment = Allot(property, classification, b, "2015-01-01");
    var ownB = PropertyOwnership.FromAllotment(OwnershipId.New(), property, allotment, b, Owned, 100, D("2015-03-01"), "LTR-1", null);
    var record = Record(property, new[] { ownB }, new[] { b, d });

    var transfer = Initiate(record, "2026-10-01", "TRF-00009", Gives(b, 40), Gets(d, 40));
    var change = Approve(transfer, record);

    var remainder = change.Opened.Single(o => o.OwnerId == b.Id);
    Assert.Equal(60, remainder.OwnershipSharePct);
    Assert.Equal(allotment.Id, remainder.AcquiredViaAllotmentId);
    Assert.Null(remainder.AcquiredViaTransferId);
    Assert.Equal("LTR-1", remainder.ReferenceNo);
    Assert.Equal("Remainder after TRF-00009.", remainder.Remarks);

    var all = record.Ownerships.Concat(change.Opened).ToList();
    Assert.Equal(D("2015-03-01"), OwnershipHistory.HoldingSince(remainder, all));

    var bought = change.Opened.Single(o => o.OwnerId == d.Id);
    Assert.Equal(transfer.Id, bought.AcquiredViaTransferId);
    Assert.Equal(D("2026-10-01"), OwnershipHistory.HoldingSince(bought, all));
  }

  [Fact]
  public void A_receiver_who_already_holds_keeps_one_row_and_their_holding_date()
  {
    var (property, _) = NewPlot();
    PropertyOwner b = Owner("B"), c = Owner("C");
    var ownC = Own(property, c, 40, "2015-05-01");
    var record = Record(property, new[] { Own(property, b, 60), ownC }, new[] { b, c });

    var change = Approve(Initiate(record, "2026-10-01", "TRF-00010", Gives(b, 20), Gets(c, 20)), record);

    var merged = change.Opened.Single(o => o.OwnerId == c.Id);
    Assert.Equal(60, merged.OwnershipSharePct);
    Assert.Equal(OwnershipStatus.Ended, ownC.OwnershipStatus);
    Assert.Equal(D("2015-05-01"), OwnershipHistory.HoldingSince(merged, record.Ownerships.Concat(change.Opened)));
  }

  // ---- disputed shares ----

  [Fact]
  public void A_disputed_share_cannot_be_given()
  {
    var (property, _) = NewPlot();
    PropertyOwner b = Owner("B"), d = Owner("D");
    var ownD = Own(property, d, 30);
    var record = Record(property, new[] { Own(property, b, 70), ownD }, new[] { b, d });
    ownD.MarkDisputed(property, null);

    Assert.Equal("D's 30% is disputed; resolve the dispute before transferring it.",
      Message(() => Initiate(record, "2026-10-01", "TRF-00001", Gives(d, 30), Gets(b, 30))));
  }

  [Fact]
  public void A_share_disputed_after_initiation_blocks_the_approval()
  {
    var (property, _) = NewPlot();
    PropertyOwner b = Owner("B"), d = Owner("D");
    var ownB = Own(property, b, 100);
    var record = Record(property, new[] { ownB }, new[] { b, d });
    var transfer = Initiate(record, "2026-10-01", "TRF-00001", Gives(b, 50), Gets(d, 50));

    ownB.MarkDisputed(property, null);

    Assert.Contains("disputed", Message(() => Approve(transfer, record)));
  }

  [Fact]
  public void A_receivers_disputed_share_stays_apart_and_disputed()
  {
    var (property, _) = NewPlot();
    PropertyOwner b = Owner("B"), d = Owner("D");
    var ownD = Own(property, d, 30);
    var record = Record(property, new[] { Own(property, b, 70), ownD }, new[] { b, d });
    ownD.MarkDisputed(property, null);

    var change = Approve(Initiate(record, "2026-10-01", "TRF-00001", Gives(b, 20), Gets(d, 20)), record);

    Assert.Equal(OwnershipStatus.Disputed, ownD.OwnershipStatus);
    Assert.Equal(20, change.Opened.Single(o => o.OwnerId == d.Id).OwnershipSharePct);
    Assert.Equal(100, OwnershipShares.Total(record.Ownerships.Concat(change.Opened)));
  }

  // ---- charged (mortgaged) shares ----

  [Fact]
  public void A_mortgaged_share_cannot_be_transferred_or_ended_until_released()
  {
    var (property, _) = NewPlot();
    PropertyOwner b = Owner("B"), d = Owner("D");
    var ownB = Own(property, b, 100);
    var mortgage = Charge(property, ownB);
    var record = Record(property, new[] { ownB }, new[] { b, d }, charges: new[] { mortgage });

    Assert.Equal("B's share is under the charge held by Bank of Khyber (ref MRT-1); release it before the share is transferred.",
      Message(() => Initiate(record, "2026-10-01", "TRF-00001", Gives(b, 50), Gets(d, 50))));
    Assert.Contains("release it before the ownership is ended", Message(() => ownB.EndHolding(D("2026-10-01"), "ORDER-1", null, record)));

    mortgage.Release(property, D("2026-10-02"), "REL-1", Today);
    var released = Record(property, new[] { ownB }, new[] { b, d }, charges: new[] { mortgage });
    Initiate(released, "2026-10-03", "TRF-00001", Gives(b, 50), Gets(d, 50));
  }

  [Fact]
  public void A_charge_on_the_whole_property_stops_every_transfer()
  {
    var (property, _) = NewPlot();
    PropertyOwner b = Owner("B"), d = Owner("D");
    var ownB = Own(property, b, 100);
    var record = Record(property, new[] { ownB }, new[] { b, d }, charges: new[] { Charge(property, null, "National Bank", null) });

    Assert.Contains("PROP-00001 is under the charge held by National Bank on the whole property",
      Message(() => Initiate(record, "2026-10-01", "TRF-00001", Gives(b, 50), Gets(d, 50))));
  }

  [Fact]
  public void A_charge_is_registered_only_against_a_current_share()
  {
    var (property, _) = NewPlot();
    var ended = Own(property, Owner("B"), 100);
    ended.End(D("2025-01-01"));

    Assert.Contains("ended on 2025-01-01", Message(() => Charge(property, ended)));
  }

  // ---- owners and acts ----

  [Fact]
  public void Every_party_must_be_an_active_owner()
  {
    var (property, _) = NewPlot();
    PropertyOwner b = Owner("B"), d = Owner("D");
    var record = Record(property, new[] { Own(property, b, 100) }, new[] { b, d });
    var transfer = Initiate(record, "2026-10-01", "TRF-00001", Gives(b, 100), Gets(d, 100));

    d.Deactivate(new PropertyOwner.Commitments([], [], []));

    Assert.Contains("is inactive; reactivate the owner record", Message(() => Initiate(record, "2026-10-01", "TRF-00002", Gives(b, 100), Gets(d, 100))));
    Assert.Contains("is inactive", Message(() => Approve(transfer, record)));
  }

  [Fact]
  public void A_waiting_transfer_is_rejected_or_cancelled_with_a_reason()
  {
    var (property, _) = NewPlot();
    PropertyOwner b = Owner("B"), d = Owner("D");
    var record = Record(property, new[] { Own(property, b, 100) }, new[] { b, d });

    var rejected = Initiate(record, "2026-10-01", "TRF-00001", Gives(b, 100), Gets(d, 100));
    Assert.Contains("Rejection reason is required", Message(() => rejected.Reject(property, " ", "officer", Today)));
    rejected.Reject(property, "Stamp duty not paid.", "officer", Today);
    Assert.Equal(TransferStatus.Rejected, rejected.TransferStatus);
    Assert.Equal("Stamp duty not paid.", rejected.ClosingReason);
    Assert.Equal("officer", rejected.ClosedBy);
    Assert.Equal(Today, rejected.ClosedOn);
    Assert.Contains("rejected", Message(() => Approve(rejected, record)));
    Assert.Contains("Only a transfer waiting for approval can be rejected", Message(() => rejected.Reject(property, "again", "officer", Today)));

    var cancelled = Initiate(record, "2026-10-01", "TRF-00002", Gives(b, 100), Gets(d, 100));
    Assert.Contains("Cancellation reason is required", Message(() => cancelled.Cancel(property, "", "clerk1", Today)));
    cancelled.Cancel(property, "Buyer withdrew.", "clerk1", Today);
    Assert.Equal(TransferStatus.Cancelled, cancelled.TransferStatus);
    Assert.Contains("already cancelled", Message(() => cancelled.Cancel(property, "again", "clerk1", Today)));
  }

  [Fact]
  public void Nothing_is_written_on_a_deactivated_property()
  {
    var (property, _) = NewPlot();
    PropertyOwner b = Owner("B"), d = Owner("D");
    var ownB = Own(property, b, 100);
    var record = Record(property, new[] { ownB }, new[] { b, d });
    var transfer = Initiate(record, "2026-10-01", "TRF-00001", Gives(b, 100), Gets(d, 100));

    property.Deactivate();

    Assert.Contains("inactive", Message(() => Approve(transfer, record)));
    Assert.Contains("inactive", Message(() => transfer.Cancel(property, "x", "clerk1", Today)));
    Assert.Contains("inactive", Message(() => ownB.MarkDisputed(property, null)));
    Assert.Contains("inactive", Message(() => ownB.EndHolding(D("2026-10-01"), "ORDER-1", null, record)));
  }

  [Fact]
  public void Ending_a_share_is_refused_in_the_future_and_while_it_is_being_transferred()
  {
    var (property, _) = NewPlot();
    PropertyOwner b = Owner("B"), d = Owner("D");
    var ownB = Own(property, b, 100);
    var pending = Initiate(Record(property, new[] { ownB }, new[] { b, d }), "2026-10-01", "TRF-00041", Gives(b, 50), Gets(d, 50));
    var record = Record(property, new[] { ownB }, new[] { b, d }, pending: new[] { pending });

    Assert.Contains("cannot end in the future", Message(() => ownB.EndHolding(D("2026-10-14"), "ORDER-1", null, record)));
    Assert.Equal("B's share is being transferred in TRF-00041; cancel or approve that first.",
      Message(() => ownB.EndHolding(D("2026-10-10"), "ORDER-1", null, record)));
  }

  [Fact]
  public void Adding_an_owner_checks_duplicates_the_100_percent_and_dates()
  {
    var (property, _) = NewPlot();
    PropertyOwner b = Owner("B"), c = Owner("C");
    var ownB = Own(property, b, 60, "2015-01-01");
    ownB.MarkDisputed(property, null);
    var record = Record(property, new[] { ownB }, new[] { b, c });

    Assert.Contains("B already holds 60%", Message(() =>
      PropertyOwnership.AddOwner(OwnershipId.New(), b, Owned, 10, D("2026-01-01"), null, null, null, record)));
    Assert.Contains("Only 40%", Message(() =>
      PropertyOwnership.AddOwner(OwnershipId.New(), c, Owned, 50, D("2026-01-01"), null, null, null, record)));
    Assert.Contains("cannot start in the future", Message(() =>
      PropertyOwnership.AddOwner(OwnershipId.New(), c, Owned, 40, D("2026-10-14"), null, null, null, record)));

    var added = PropertyOwnership.AddOwner(OwnershipId.New(), c, Owned, 40, D("2026-01-01"), null, null, null, record);
    Assert.Equal(40, added.OwnershipSharePct);
  }
}
