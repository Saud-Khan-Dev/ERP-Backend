using static Fixture;
using static OwnershipFixture;

/// The rules added by the 2026-10-20 audit: ownership changes only by approval, maker-checker, enforced charges,
/// allotments on private land and their effective date, one agreement of a kind at a time, terms frozen once in force,
/// fines with receipts and reasons, decided cases final, and recorded events never in the future.
public class OwnershipApprovalRuleTests
{
  [Fact]
  public void Add_owner_is_refused_while_an_allotment_is_in_force()
  {
    var (property, classification) = NewPlot();
    var allottee = Owner("Shazia Bibi");
    var allotment = Allot(property, classification, allottee, no: "ALT-00039");
    var someone = Owner("Someone");
    var record = new OwnershipRecord(property, Array.Empty<PropertyOwnership>(), new[] { someone }, Array.Empty<PropertyTransfer>(),
      Array.Empty<PropertyEncumbrance>(), Today, new PropertyAllotment.InForce(allotment, "Shazia Bibi"));

    Assert.Contains("only by confirming the allottee as owner",
      Message(() => PropertyOwnership.AddOwner(OwnershipId.New(), someone, Owned, 100, D("2026-01-01"), null, null, null, record)));
  }

  [Fact]
  public void A_property_nobody_held_has_no_holder_history()
  {
    var (property, _) = NewPlot();
    var b = Owner("B");

    Assert.False(Record(property, Array.Empty<PropertyOwnership>(), new[] { b }).HasHolderHistory);
    Assert.True(Record(property, new[] { Own(property, b, 100) }, new[] { b }).HasHolderHistory);
  }

  [Fact]
  public void Ending_a_share_needs_the_order_it_ends_under()
  {
    var (property, _) = NewPlot();
    var b = Owner("B");
    var ownB = Own(property, b, 100);
    var record = Record(property, new[] { ownB }, new[] { b });

    Assert.Contains("Give the order or reference", Message(() => ownB.EndHolding(D("2026-10-01"), null, null, record)));

    ownB.EndHolding(D("2026-10-01"), "DG/EST/77", "Surrendered to GDA.", record);
    Assert.Equal(OwnershipStatus.Ended, ownB.OwnershipStatus);
    Assert.Equal("DG/EST/77", ownB.EndReferenceNo);
  }

  [Fact]
  public void A_share_confirmed_through_an_allotment_in_force_is_not_ended_by_hand()
  {
    var (property, classification) = NewPlot();
    var allottee = Owner("Shazia Bibi");
    var allotment = Allot(property, classification, allottee, "2025-01-01", "ALT-00007");
    var confirmed = allotment.ConfirmOwnership(OwnershipId.New(), Active, allottee, Owned, 100, D("2025-02-01"), null, null,
      Record(property, Array.Empty<PropertyOwnership>(), new[] { allottee }));
    var record = new OwnershipRecord(property, new[] { confirmed }, new[] { allottee }, Array.Empty<PropertyTransfer>(),
      Array.Empty<PropertyEncumbrance>(), Today, new PropertyAllotment.InForce(allotment, "Shazia Bibi"));

    Assert.Equal("This share was confirmed through ALT-00007, which is still in force; surrender or cancel the allotment to end it.",
      Message(() => confirmed.EndHolding(D("2026-10-01"), "ORDER-1", null, record)));
  }
}

public class MakerCheckerRuleTests
{
  private static (PropertyTransfer Transfer, OwnershipRecord Record) Pending(Guid startedBy)
  {
    var (property, _) = NewPlot();
    PropertyOwner b = Owner("B"), d = Owner("D");
    var record = Record(property, new[] { Own(property, b, 100) }, new[] { b, d });
    var transfer = Initiate(record, "2026-10-01", "TRF-00051", Gives(b, 100), Gets(d, 100));
    transfer.CreatedBy = startedBy;
    return (transfer, record);
  }

  // Whether the person who started a transfer may approve it is the PROPERTY_TRANSFER separation-of-duties rule, which
  // Administration switches and the approve handler checks (IDutyRules); the domain no longer fixes it.

  [Fact]
  public void A_past_transfer_entered_for_a_second_officer_waits_with_the_approval_read_off_the_paper()
  {
    var (property, _) = NewPlot();
    PropertyOwner b = Owner("B"), d = Owner("D");
    var ownB = Own(property, b, 100);
    var record = Record(property, new[] { ownB }, new[] { b, d });

    var transfer = PropertyTransfer.RecordForApproval(TransferId.New(), BusinessCode.Of("TRF-00060"), Sale, D("2021-05-01"), "MUT-7", null, null, null,
      new[] { Gives(b, 100), Gets(d, 100) }, D("2021-05-03"), "DG/12", "CLR-00009", record);

    Assert.Equal(TransferStatus.Initiated, transfer.TransferStatus);
    Assert.True(transfer.IsRecordedPast);
    Assert.Equal((D("2021-05-03"), "DG/12", "CLR-00009"), (transfer.ApprovalDate, transfer.ApprovalOrderRef, transfer.NoDuesCertificateNo));
    Assert.Null(ownB.EffectiveTo);
    Assert.Null(transfer.ApprovedByUserId);

    // the second officer approves it as recorded: ownership moves then
    var change = transfer.Approve(new Approver(Guid.NewGuid(), null, "director.estate"), transfer.ApprovalDate!.Value, transfer.ApprovalOrderRef,
      transfer.NoDuesCertificateNo, "director.estate", record, Owned);
    Assert.Equal(TransferStatus.Completed, transfer.TransferStatus);
    Assert.False(transfer.IsRecordedPast);
    Assert.Equal(D("2021-05-01"), ownB.EffectiveTo);
    Assert.Single(change.Opened);
  }

  [Fact]
  public void A_past_transfer_for_approval_is_never_future_dated_and_its_approval_date_follows_the_transfer()
  {
    var (property, _) = NewPlot();
    PropertyOwner b = Owner("B"), d = Owner("D");
    var record = Record(property, new[] { Own(property, b, 100) }, new[] { b, d });

    PropertyTransfer Record_(string date, string? approved) => PropertyTransfer.RecordForApproval(TransferId.New(), BusinessCode.Of("TRF-00061"), Sale,
      D(date), null, null, null, null, new[] { Gives(b, 100), Gets(d, 100) }, approved is null ? null : D(approved), null, null, record);

    Assert.Contains("cannot be dated in the future", Message(() => Record_("2026-10-14", null)));
    Assert.Contains("before its transfer date", Message(() => Record_("2026-05-01", "2026-04-30")));
    Assert.Contains("cannot be in the future", Message(() => Record_("2026-05-01", "2026-10-14")));
    Assert.Equal(D("2026-05-01"), Record_("2026-05-01", null).ApprovalDate);
  }

  [Fact]
  public void The_approver_is_the_signed_in_officer_with_the_no_dues_certificate()
  {
    var (transfer, record) = Pending(Guid.NewGuid());
    var employee = Guid.NewGuid();
    var approver = new Approver(Guid.NewGuid(), employee, "director.estate");

    transfer.Approve(approver, Today, "DG/77", "CLR-00017", "director.estate", record, Owned);

    Assert.Equal(("director.estate", approver.UserId, (Guid?)employee, "CLR-00017"),
      (transfer.ApprovedBy, transfer.ApprovedByUserId, transfer.ApprovedByEmployeeId, transfer.NoDuesCertificateNo));
  }

  [Fact]
  public void A_transfer_dated_more_than_90_days_ahead_needs_a_confirmation()
  {
    var (property, _) = NewPlot();
    PropertyOwner b = Owner("B"), d = Owner("D");
    var record = Record(property, new[] { Own(property, b, 100) }, new[] { b, d });
    var far = Today.AddDays(PropertyTransfer.FarFutureDays + 1);

    Assert.Contains("more than 90 days ahead", Message(() => PropertyTransfer.Initiate(TransferId.New(), BusinessCode.Of("TRF-00052"), Sale, far,
      null, null, null, null, new[] { Gives(b, 100), Gets(d, 100) }, record)));

    var confirmed = PropertyTransfer.Initiate(TransferId.New(), BusinessCode.Of("TRF-00052"), Sale, far,
      null, null, null, null, new[] { Gives(b, 100), Gets(d, 100) }, record, confirmFarFuture: true);
    Assert.Equal(TransferStatus.Initiated, confirmed.TransferStatus);
  }
}

public class EnforcedChargeRuleTests
{
  private static PropertyEncumbrance BankCharge(Property property, PropertyOwnership? share, PropertyOwner bank) =>
      PropertyEncumbrance.Register(EncumbranceId.New(), property, Master<EncumbranceType>("MORTGAGE"), share, "Bank of Khyber", bank, "MRT-9",
        500_000m, D("2024-01-01"), null, null);

  [Fact]
  public void Enforcing_a_charge_needs_its_holder_on_record()
  {
    var (property, _) = NewPlot();
    var b = Owner("B");
    var charge = Charge(property, Own(property, b, 100));

    Assert.Contains("Link Bank of Khyber to an owner record first", Message(() => charge.Enforce(property, null)));
  }

  [Fact]
  public void An_enforced_charge_blocks_transfers_except_to_its_holder()
  {
    var (property, _) = NewPlot();
    PropertyOwner b = Owner("B"), d = Owner("D"), bank = Company("Bank of Khyber");
    var ownB = Own(property, b, 100);
    var charge = BankCharge(property, ownB, bank);
    charge.Enforce(property, "Foreclosed.");
    var record = Record(property, new[] { ownB }, new[] { b, d, bank }, charges: new[] { charge });

    Assert.Contains("which was enforced; it can only be transferred to Bank of Khyber",
      Message(() => Initiate(record, "2026-10-01", "TRF-00061", Gives(b, 100), Gets(d, 100))));
    Assert.Contains("which was enforced; the share can only be transferred to Bank of Khyber",
      Message(() => ownB.EndHolding(D("2026-10-01"), "ORDER-1", null, record)));

    Assert.Equal(TransferStatus.Initiated, Initiate(record, "2026-10-01", "TRF-00062", Gives(b, 100), Gets(bank, 100)).TransferStatus);
  }

  [Fact]
  public void A_transfer_to_the_holder_of_an_enforced_charge_settles_it()
  {
    var (property, _) = NewPlot();
    PropertyOwner b = Owner("B"), bank = Company("Bank of Khyber");
    var ownB = Own(property, b, 100);
    var charge = BankCharge(property, ownB, bank);
    charge.Enforce(property, null);
    var record = Record(property, new[] { ownB }, new[] { b, bank }, charges: new[] { charge });

    Approve(Initiate(record, "2026-10-01", "TRF-00063", Gives(b, 100), Gets(bank, 100)), record);

    Assert.Equal((EncumbranceStatus.Released, "TRF-00063", D("2026-10-01")), (charge.Status, charge.ReleaseReferenceNo, charge.ReleaseDate));
  }

  [Fact]
  public void A_charge_cannot_start_in_the_future()
  {
    var (property, _) = NewPlot();

    Assert.Contains("cannot start in the future", Message(() => PropertyEncumbrance.Register(EncumbranceId.New(), property,
      Master<EncumbranceType>("MORTGAGE"), null, "Bank", null, null, null, Today.AddDays(1), null, null, Today)));
  }
}

public class AllotmentLandRuleTests
{
  [Fact]
  public void A_plot_held_privately_cannot_be_allotted()
  {
    var (property, classification) = NewPlot();

    Assert.Equal("PROP-00001 is held by Saad (61%) and Saud (39%); only land GDA holds can be allotted. If GDA has acquired it, record that transfer first, or correct the classification.",
      Message(() => PropertyAllotment.Allot(AllotmentId.New(), property, classification, Owner("Shazia Bibi"), BusinessCode.Of("ALT-00039"), Plot, Active,
        D("2025-01-01"), null, null, null, null, null, null, Today,
        new[] { new PropertyAllotment.HeldShare("Saud", 39, false), new PropertyAllotment.HeldShare("Saad", 61, false) })));
  }

  [Fact]
  public void Land_a_government_body_holds_can_be_allotted()
  {
    var (property, classification) = NewPlot();

    var allotment = PropertyAllotment.Allot(AllotmentId.New(), property, classification, Owner("Shazia Bibi"), BusinessCode.Of("ALT-00040"), Plot, Active,
      D("2025-01-01"), null, null, null, null, null, null, Today, new[] { new PropertyAllotment.HeldShare("Galiyat Development Authority", 100, true) });

    Assert.True(allotment.IsInForceOn(Active, Today));
  }

  [Fact]
  public void An_allotment_is_in_force_only_from_its_effective_date()
  {
    var (property, classification) = NewPlot();
    var allotment = PropertyAllotment.Allot(AllotmentId.New(), property, classification, Owner("Shazia Bibi"), BusinessCode.Of("ALT-00041"), Plot, Active,
      D("2026-10-01"), D("2027-01-13"), null, null, null, null, null, Today);

    Assert.Equal(D("2027-01-13"), allotment.TakesEffectOn);
    Assert.False(allotment.IsInForceOn(Active, Today));
    Assert.True(allotment.IsInForceOn(Active, D("2027-01-13")));
  }

  [Fact]
  public void Confirm_as_owner_waits_until_the_allotment_takes_effect()
  {
    var (property, classification) = NewPlot();
    var allottee = Owner("Shazia Bibi");
    var allotment = PropertyAllotment.Allot(AllotmentId.New(), property, classification, allottee, BusinessCode.Of("ALT-00042"), Plot, Active,
      D("2026-10-01"), D("2027-01-13"), null, null, null, null, null, Today);

    Assert.Equal("ALT-00042 takes effect on 2027-01-13; the allottee can be confirmed as owner from that day.",
      Message(() => allotment.ConfirmOwnership(OwnershipId.New(), Active, allottee, Owned, 100, Today, null, null,
        Record(property, Array.Empty<PropertyOwnership>(), new[] { allottee }))));
  }

  [Fact]
  public void The_classification_cannot_change_while_allotted()
  {
    var (property, classification) = NewPlot();
    var allotment = Allot(property, classification, Owner("Shazia Bibi"), no: "ALT-00043");

    Assert.Contains("its classification cannot change while the allotment is in force", Message(() => property.UpdateDetails(
      Name.Of("Plot 7"), Master<Town>("NATHIA_GALI"), Master<PropertyType>("RESIDENTIAL"), Master<PropertyClassification>("PRIVATE_PROPERTY"),
      null, null, null, null, new PropertyAllotment.InForce(allotment, "Shazia Bibi"))));
  }
}

public class AgreementOverlapRuleTests
{
  private static readonly MasterId Parking = MasterId.New();
  private static readonly MasterId Cleaning = MasterId.New();

  [Fact]
  public void Two_leases_cannot_overlap_on_one_property()
  {
    var running = new[] { new AgreementSpan(AgreementSpanKind.Lease, "LSE-00005", "Ali Khan", D("2026-01-01"), D("2030-12-31")) };

    Assert.Equal("LSE-00005 to Ali Khan already runs from 2026-01-01 to 2030-12-31 on this property; a second lease cannot overlap it. Renew or end LSE-00005 first, or start the new lease after 2030-12-31.",
      Message(() => AgreementOverlap.Check(new AgreementSpan(AgreementSpanKind.Lease, "LSE-00006", "Sara", D("2028-01-01"), D("2032-12-31")), running)));

    Assert.Empty(AgreementOverlap.Check(new AgreementSpan(AgreementSpanKind.Lease, "LSE-00006", "Sara", D("2031-01-01"), D("2035-12-31")), running));
  }

  [Fact]
  public void A_renewal_does_not_overlap_the_agreement_it_renews()
  {
    var running = new[] { new AgreementSpan(AgreementSpanKind.Rental, "RNT-00025", "Tenant", D("2024-01-01"), null) };

    Assert.Empty(AgreementOverlap.Check(new AgreementSpan(AgreementSpanKind.Rental, "RNT-00026", "Tenant", D("2026-01-01"), null), running, except: "RNT-00025"));
  }

  [Fact]
  public void Contracts_of_different_types_may_run_side_by_side()
  {
    var running = new[] { new AgreementSpan(AgreementSpanKind.Contract, "CON-00001", "ParkCo", D("2026-01-01"), null, Parking) };

    Assert.Empty(AgreementOverlap.Check(new AgreementSpan(AgreementSpanKind.Contract, "CON-00002", "CleanCo", D("2026-02-01"), null, Cleaning), running));
    Assert.Contains("a second contract of this type cannot overlap it",
      Message(() => AgreementOverlap.Check(new AgreementSpan(AgreementSpanKind.Contract, "CON-00003", "ParkCo 2", D("2026-03-01"), null, Parking), running)));
  }

  [Fact]
  public void Agreements_of_other_kinds_only_warn()
  {
    var running = new[] { new AgreementSpan(AgreementSpanKind.Allotment, "ALT-00007", "Shazia Bibi", D("2025-01-01"), null) };

    var warning = Assert.Single(AgreementOverlap.Check(new AgreementSpan(AgreementSpanKind.Rental, "RNT-00030", "Tenant", D("2026-01-01"), D("2026-12-31")), running));
    Assert.Equal("This property is also allotted to Shazia Bibi (ALT-00007) from 2025-01-01 with no end date.", warning);
  }
}

public class AgreementTermsRuleTests
{
  private static readonly RentalStatus RentDraft = Master<RentalStatus>(SystemMasterCodes.Draft);
  private static readonly RentalStatus RentActive = Master<RentalStatus>(SystemMasterCodes.Active);
  private static readonly RentalStatus RentEnded = Master<RentalStatus>(SystemMasterCodes.Ended);
  private static readonly RentalStatus RentExpired = Master<RentalStatus>(SystemMasterCodes.Expired);
  private static readonly RentalStatus RentTerminated = Master<RentalStatus>(SystemMasterCodes.Terminated);
  private static readonly RentalType Shop = Master<RentalType>("SHOP_RENT");

  private static PropertyRental.Terms RentTerms(decimal rent = 18500, string start = "2024-01-01", string? end = "2028-12-31", decimal? increase = 10, string? reference = "AGR-1") =>
      new(Shop, D(start), end is null ? null : D(end), rent, RentFrequency.Monthly, 50000, increase, reference, null, null);

  private static PropertyRental Rental(RentalStatus status, PropertyRental.Terms? terms = null) =>
      PropertyRental.Create(RentalId.New(), Property(), Owner("Tenant"), BusinessCode.Of("RNT-00025"), status, terms ?? RentTerms());

  [Fact]
  public void An_active_rental_keeps_its_terms_and_corrects_only_reference_and_remarks()
  {
    var rental = Rental(RentActive);

    Assert.Contains("its terms are history", Message(() => rental.UpdateTerms(RentActive, RentTerms(rent: 15000))));

    rental.UpdateTerms(RentActive, RentTerms(reference: "AGR-1/corrected"));
    Assert.Equal(("AGR-1/corrected", 18500m), (rental.AgreementReference, rental.RentAmount));
  }

  [Fact]
  public void A_draft_rental_takes_any_change_and_then_activates()
  {
    var rental = Rental(RentDraft);
    rental.UpdateTerms(RentDraft, RentTerms(rent: 20000));
    rental.Activate(RentDraft, RentActive);

    Assert.Equal((20000m, RentActive.Id), (rental.RentAmount, rental.RentalStatusId));
    Assert.Contains("Only a draft rental can be activated", Message(() => rental.Activate(RentActive, RentActive)));
  }

  [Fact]
  public void A_rental_termination_is_terminated_dated_not_in_the_future_nor_after_its_end()
  {
    var today = D("2026-10-13");

    Assert.Contains("cannot be in the future", Message(() => Rental(RentActive).Terminate(RentActive, RentTerminated, D("2026-10-20"), "order", today)));
    Assert.Contains("ends on 2028-12-31 anyway", Message(() => Rental(RentActive).Terminate(RentActive, RentTerminated, D("2029-01-05"), "order", D("2029-02-01"))));

    var rental = Rental(RentActive);
    rental.Terminate(RentActive, RentTerminated, D("2026-10-01"), "DG order 12", today);
    Assert.Equal(RentTerminated.Id, rental.RentalStatusId);
    Assert.Throws<DomainException>(() => Rental(RentActive).Terminate(RentActive, RentEnded, D("2026-10-01"), "order", today));
  }

  [Fact]
  public void A_draft_that_will_not_go_ahead_is_withdrawn()
  {
    var withdrawn = Master<RentalStatus>(SystemMasterCodes.Withdrawn);
    var rental = Rental(RentDraft);

    Assert.Contains("Only a draft can be withdrawn", Message(() => Rental(RentActive).WithdrawDraft(RentActive, withdrawn, "x")));

    rental.WithdrawDraft(RentDraft, withdrawn, "Tenant backed out");
    Assert.Equal((withdrawn.Id, "Tenant backed out"), (rental.RentalStatusId, rental.TerminationReason));
  }

  [Fact]
  public void An_agreement_that_has_not_started_is_called_off_today()
  {
    var today = D("2026-10-13");
    var future = Rental(RentActive, RentTerms(start: "2027-01-01", end: "2027-12-31"));

    Assert.Contains("has not started yet", Message(() => future.Terminate(RentActive, RentTerminated, D("2026-10-01"), "order", today)));

    future.Terminate(RentActive, RentTerminated, today, "Allotment cancelled before the rental began", today);
    Assert.Equal((RentTerminated.Id, today), (future.RentalStatusId, future.TerminationDate!.Value));
    Assert.Contains("cannot end before it started", Message(() => Rental(RentActive).Terminate(RentActive, RentTerminated, D("2023-12-01"), "order", today)));
  }

  [Fact]
  public void A_rental_expires_only_after_its_end_date()
  {
    Assert.Contains("ends on 2028-12-31", Message(() => Rental(RentActive).Expire(RentActive, RentExpired, D("2028-12-31"))));
    Assert.Contains("has no end date", Message(() => Rental(RentActive, RentTerms(end: null)).Expire(RentActive, RentExpired, D("2030-01-01"))));

    var rental = Rental(RentActive);
    rental.Expire(RentActive, RentExpired, D("2029-01-01"));
    Assert.Equal(RentExpired.Id, rental.RentalStatusId);
  }

  [Fact]
  public void The_current_rent_rises_by_the_annual_increase_for_each_full_year()
  {
    var rental = Rental(RentActive, RentTerms(rent: 18500, start: "2020-03-01", end: null, increase: 10));

    Assert.Equal(18500m, rental.CurrentRent(D("2021-02-28")));
    Assert.Equal(20350m, rental.CurrentRent(D("2021-03-01")));
    Assert.Equal(decimal.Round(18500m * 1.61051m, 2), rental.CurrentRent(D("2025-03-01")));
  }

  [Fact]
  public void A_contract_moves_only_through_its_acts()
  {
    var active = Master<ContractStatus>(SystemMasterCodes.Active);
    var suspended = Master<ContractStatus>("SUSPENDED");
    var contract = PropertyOutsourcing.Create(OutsourcingId.New(), Property(), Company("ParkCo"), BusinessCode.Of("CON-00001"), active,
      new PropertyOutsourcing.Terms(Master<OutsourcingType>("PARKING"), D("2026-01-01"), D("2026-12-31"), null, 100000, AmountFrequency.Monthly, null, null, null,
        MoneyDirection.Receivable));

    Assert.Contains("changes status only through its acts", Message(() => contract.ChangeStatus(active, suspended, D("2026-06-01"))));
    Assert.Contains("its terms are history", Message(() => contract.Update(active, new PropertyOutsourcing.Terms(Master<OutsourcingType>("PARKING"),
      D("2026-01-01"), D("2026-12-31"), null, 50000, AmountFrequency.Monthly, null, null, null, MoneyDirection.Receivable))));
  }

  [Fact]
  public void A_contract_renewal_ends_the_old_one_the_day_before_it_starts()
  {
    var active = Master<ContractStatus>(SystemMasterCodes.Active);
    var renewed = Master<ContractStatus>(SystemMasterCodes.Renewed);
    var type = Master<OutsourcingType>("PARKING");
    var contract = PropertyOutsourcing.Create(OutsourcingId.New(), Property(), Company("ParkCo"), BusinessCode.Of("CON-00001"), active,
      new PropertyOutsourcing.Terms(type, D("2026-01-01"), null, null, 100000, AmountFrequency.Monthly, null, null, null, MoneyDirection.Receivable));

    var renewal = contract.Renew(active, renewed, active, OutsourcingId.New(), BusinessCode.Of("CON-00002"), Company("ParkCo"),
      new PropertyOutsourcing.Terms(type, D("2027-01-01"), null, null, 120000, AmountFrequency.Monthly, null, null, null));

    Assert.Equal((renewed.Id, D("2026-12-31")), (contract.ContractStatusId, contract.ContractEndDate));
    Assert.Equal((contract.Id, MoneyDirection.Receivable), (renewal.RenewedFromContractId!, renewal.MoneyDirection!.Value));
  }

  [Fact]
  public void An_active_lease_corrects_only_its_reference_and_remarks()
  {
    var active = Master<LeaseStatus>(SystemMasterCodes.Active);
    var type = Master<LeaseType>("COMMERCIAL");
    PropertyLease.Terms Terms(string? reference, decimal amount = 250000) =>
        new(type, D("2026-01-01"), D("2030-12-31"), 5, null, amount, AmountFrequency.Annual, null, reference, null, true, null);
    var lease = PropertyLease.Create(LeaseId.New(), Property(), Owner("Lessee"), BusinessCode.Of("LSE-00001"), active, Terms("AGR-9"));

    lease.UpdateTerms(active, Terms("AGR-9A"));
    Assert.Equal("AGR-9A", lease.AgreementReference);
    Assert.Contains("its terms are history", Message(() => lease.UpdateTerms(active, Terms("AGR-9A", 200000))));
  }

  [Fact]
  public void A_lease_term_that_does_not_match_its_dates_warns()
  {
    var lease = PropertyLease.Create(LeaseId.New(), Property(), Owner("Lessee"), BusinessCode.Of("LSE-00001"), Master<LeaseStatus>(SystemMasterCodes.Active),
      new PropertyLease.Terms(Master<LeaseType>("COMMERCIAL"), D("2026-01-01"), D("2028-12-31"), 5, null, null, null, null, null, null, false, null));

    Assert.Contains("does not match the dates", Assert.Single(lease.TermWarnings()));
  }
}

public class FineRuleTests
{
  private static readonly DateOnly Today = D("2026-10-13");

  private static AgreementViolation Violation(out PropertyOwner lessee, out PropertyLease lease)
  {
    var property = Property();
    lessee = Owner("Lessee");
    lease = PropertyLease.Create(LeaseId.New(), property, lessee, BusinessCode.Of("LSE-00001"), Master<LeaseStatus>(SystemMasterCodes.Active),
      new PropertyLease.Terms(Master<LeaseType>("COMMERCIAL"), D("2026-01-01"), D("2030-12-31"), null, null, null, null, null, null, null, false, null));
    return AgreementViolation.Record(ViolationId.New(), property, Master<AgreementType>(SystemMasterCodes.AgreementLease),
      new AgreementViolation.Agreement(lease, null, null), lessee, D("2026-03-01"), "Built without approval",
      new AgreementViolation.Notice(null, null, null), Array.Empty<AgreementViolation>(), true, null, Today, out _);
  }

  [Fact]
  public void A_paid_fine_needs_its_receipt_and_date_and_is_final()
  {
    var violation = Violation(out _, out _);
    violation.ImposeFine(50000, Guid.NewGuid(), D("2026-04-01"));

    Assert.Contains("receipt no.", Message(() => violation.ChangeFineStatus(new AgreementViolation.FineChange(FineStatus.Paid, PaidOn: D("2026-05-01")), Today, null)));
    Assert.Contains("before it was imposed", Message(() => violation.ChangeFineStatus(new AgreementViolation.FineChange(FineStatus.Paid, "RCP-1", D("2026-03-15")), Today, null)));
    Assert.Contains("cannot be in the future", Message(() => violation.ChangeFineStatus(new AgreementViolation.FineChange(FineStatus.Paid, "RCP-1", D("2026-10-20")), Today, null)));

    var clerk = Guid.NewGuid();
    violation.ChangeFineStatus(new AgreementViolation.FineChange(FineStatus.Paid, "RCP-1", D("2026-05-01")), Today, clerk);
    Assert.Equal((FineStatus.Paid, "RCP-1", D("2026-05-01"), (Guid?)clerk), (violation.FineStatus!.Value, violation.FineReceiptNo, violation.FinePaidOn!.Value, violation.FineStatusChangedBy));
    Assert.Contains("already paid", Message(() => violation.ChangeFineStatus(new AgreementViolation.FineChange(FineStatus.Waived, Reason: "x"), Today, null)));
    Assert.Contains("already paid", Message(() => violation.ImposeFine(10000, Guid.NewGuid(), Today)));
  }

  [Fact]
  public void A_waived_fine_needs_its_reason()
  {
    var violation = Violation(out _, out _);
    violation.ImposeFine(50000, Guid.NewGuid(), D("2026-04-01"));

    Assert.Contains("reason the fine is waived", Message(() => violation.ChangeFineStatus(new AgreementViolation.FineChange(FineStatus.Waived), Today, null)));

    violation.ChangeFineStatus(new AgreementViolation.FineChange(FineStatus.Waived, Reason: "Rectified within a week"), Today, null);
    Assert.Equal((FineStatus.Waived, "Rectified within a week"), (violation.FineStatus!.Value, violation.FineStatusReason));
  }

  [Fact]
  public void The_violator_must_be_a_party_to_the_agreement()
  {
    var property = Property();
    var lessee = Owner("Lessee");
    var lease = PropertyLease.Create(LeaseId.New(), property, lessee, BusinessCode.Of("LSE-00002"), Master<LeaseStatus>(SystemMasterCodes.Active),
      new PropertyLease.Terms(Master<LeaseType>("COMMERCIAL"), D("2026-01-01"), D("2030-12-31"), null, null, null, null, null, null, null, false, null));

    Assert.Contains("is not the lessee of LSE-00002", Message(() => AgreementViolation.Record(ViolationId.New(), property,
      Master<AgreementType>(SystemMasterCodes.AgreementLease), new AgreementViolation.Agreement(lease, null, null), Owner("Neighbour"),
      D("2026-03-01"), "Encroached", new AgreementViolation.Notice(null, null, null), Array.Empty<AgreementViolation>(), true, null, Today, out _)));
  }

  [Fact]
  public void A_breach_of_an_allotment_is_recorded_against_the_allottee()
  {
    var (property, classification) = NewPlot();
    var allottee = Owner("Shazia Bibi");
    var allotment = Allot(property, classification, allottee, no: "ALT-00044");

    var violation = AgreementViolation.Record(ViolationId.New(), property, Master<AgreementType>(SystemMasterCodes.AgreementAllotment),
      new AgreementViolation.Agreement(null, null, null, allotment), allottee, D("2026-03-01"), "No construction within the deadline",
      new AgreementViolation.Notice(null, null, null), Array.Empty<AgreementViolation>(), true, null, Today, out _);

    Assert.Equal((allotment.Id, false), (violation.AllotmentId!, violation.LedToCancellation));
  }
}

public class DecidedRecordRuleTests
{
  private static readonly DateOnly Today = D("2026-10-13");
  private static readonly MeasurementUnit Sqft = Unit("SQFT", 1, true);

  private static PropertyAreaRegularization.Decision Decision(RegularizationStatus status, string? on = null, string? effectiveTo = null) =>
      new(status, on is null ? null : D(on), "ORD-1", null, null, null, effectiveTo is null ? null : D(effectiveTo), null);

  [Fact]
  public void A_decided_regularization_is_final()
  {
    var regularization = PropertyAreaRegularization.Open(AreaRegularizationId.New(), Property(), Sqft, 500, D("2026-01-01"),
      Decision(RegularizationStatus.Regularized, "2026-02-01"), Today);

    Assert.Contains("the decision is final", Message(() => regularization.UpdateCase(Decision(RegularizationStatus.Pending), Today)));

    regularization.UpdateCase(Decision(RegularizationStatus.Regularized, effectiveTo: "2026-09-01"), Today);
    Assert.False(regularization.CountsTowardArea(Today));
  }

  [Fact]
  public void The_regularization_date_cannot_be_in_the_future()
  {
    Assert.Contains("cannot be in the future", Message(() => PropertyAreaRegularization.Open(AreaRegularizationId.New(), Property(), Sqft, 500, null,
      Decision(RegularizationStatus.Regularized, "2026-10-20"), Today)));
  }

  [Fact]
  public void A_hearing_is_recorded_once_it_took_place()
  {
    var pending = Master<LitigationStatus>(SystemMasterCodes.Pending);
    var litigation = PropertyLitigation.File(LitigationId.New(), Property(), "CS-1", "Civil Court Abbottabad", pending,
      new PropertyLitigation.Details("GDA v X", Master<LitigationType>("CIVIL_SUIT"), D("2026-01-01"), null, null, null), new(null, null, null, null),
      Array.Empty<PropertyLitigation.PartyInput>());

    Assert.Contains("is in the future", Message(() => litigation.RecordHearing(pending, D("2026-10-20"), null, null, null, null, null, Today)));

    var attendee = Guid.NewGuid();
    var hearing = litigation.RecordHearing(pending, D("2026-10-01"), null, null, D("2026-11-01"), "Bilal Ahmed", attendee, Today);
    Assert.Equal(("Bilal Ahmed", (Guid?)attendee), (hearing.AttendedBy, hearing.AttendedByEmployeeId));
  }

  [Fact]
  public void A_court_decision_records_how_it_came_out_and_cannot_be_in_the_future()
  {
    var pending = Master<LitigationStatus>(SystemMasterCodes.Pending);
    var decided = Master<LitigationStatus>(SystemMasterCodes.Decided);
    PropertyLitigation Case() => PropertyLitigation.File(LitigationId.New(), Property(), "CS-2", "Civil Court Abbottabad", pending,
      new PropertyLitigation.Details("GDA v Y", Master<LitigationType>("CIVIL_SUIT"), D("2026-01-01"), null, null, null), new(null, null, null, null),
      Array.Empty<PropertyLitigation.PartyInput>());

    Assert.Contains("cannot be in the future", Message(() => Case().Decide(pending, decided, D("2026-10-20"), "Decreed", CaseResult.InFavourOfGda, Today)));

    var litigation = Case();
    litigation.Decide(pending, decided, D("2026-10-01"), "Suit decreed in favour of GDA", CaseResult.InFavourOfGda, Today);
    Assert.Equal(CaseResult.InFavourOfGda, litigation.DecisionResult);
  }

  [Fact]
  public void An_appeal_decision_cannot_be_in_the_future()
  {
    var appeal = PropertyAppeal.File(AppealId.New(), Property(), BusinessCode.Of("APL-00001"),
      new PropertyAppeal.Filing(Owner("Appellant"), "DG/1", D("2026-09-01"), null, null, null, D("2026-09-10"), null, null, null),
      Array.Empty<PropertyAppeal>(), out _);

    Assert.Contains("cannot be in the future", Message(() => appeal.Decide(D("2026-10-20"), AppealOutcome.Dismissed, null, Today)));
  }

  [Fact]
  public void An_encroachment_resolution_cannot_be_in_the_future()
  {
    var encroachment = PropertyEncroachment.Record(EncroachmentId.New(), Property(), BusinessCode.Of("ENC-00001"), 100, Sqft,
      Master<EncroachmentStatus>(SystemMasterCodes.Active), D("2026-06-01"), new PropertyEncroachment.Details("X", null, null, null, null), null, Today);

    Assert.Contains("cannot be in the future", Message(() => encroachment.Resolve(Master<EncroachmentStatus>(SystemMasterCodes.Resolved),
      D("2026-10-20"), EncroachmentResolution.Removed, null, Today)));
  }

  [Fact]
  public void A_plan_approval_on_a_steep_plot_warns_and_its_covered_area_is_kept_in_square_feet()
  {
    var submitted = Master<BuildingPlanStatus>(SystemMasterCodes.Submitted);
    var approved = Master<BuildingPlanStatus>(SystemMasterCodes.Approved);
    var plan = BuildingPlan.Submit(BuildingPlanId.New(), Property(), BusinessCode.Of("BP-00001"), submitted,
      new BuildingPlan.Details(Master<BuildingPlanType>("BUILDING_PLAN"), null, D("2026-05-01"), 2, Unit("MARLA", 272.25m), 2, null, null));

    Assert.Equal(544.5m, plan.CoveredAreaBase);
    Assert.Contains("cannot be in the future", Message(() => plan.Approve(submitted, approved,
      new BuildingPlan.Approval(D("2026-10-20"), "Director", null, null, null), Today)));

    var warnings = plan.Approve(submitted, approved, new BuildingPlan.Approval(D("2026-10-01"), "Director", null, "BCA-1", null, "CLR-00018"), Today, 52m);
    Assert.Contains("above the 45° limit", Assert.Single(warnings));
    Assert.Equal("CLR-00018", plan.NoDuesCertificateNo);
  }

  [Fact]
  public void A_rejected_plan_is_under_consideration_again_after_an_allowed_appeal()
  {
    var submitted = Master<BuildingPlanStatus>(SystemMasterCodes.Submitted);
    var rejected = Master<BuildingPlanStatus>(SystemMasterCodes.Rejected);
    var plan = BuildingPlan.Submit(BuildingPlanId.New(), Property(), BusinessCode.Of("BP-00002"), submitted,
      new BuildingPlan.Details(Master<BuildingPlanType>("BUILDING_PLAN"), null, D("2026-05-01"), null, null, 2, null, null));
    plan.Reject(submitted, rejected, "Setback short");

    plan.ReopenOnAppeal(rejected, submitted, "APL-00009");
    Assert.Equal(submitted.Id, plan.BuildingPlanStatusId);
    Assert.Contains("set aside on appeal APL-00009", plan.Remarks);
  }
}

public class DeactivationRuleTests
{
  [Fact]
  public void Deactivation_records_its_reason_and_day_and_reactivation_clears_them()
  {
    var property = Property();
    property.Deactivate("Entered twice; see PROP-00012.", D("2026-10-13"));
    Assert.Equal((false, D("2026-10-13"), "Entered twice; see PROP-00012."), (property.IsActive, property.DeactivatedOn!.Value, property.DeactivationReason));

    property.Activate();
    Assert.Equal((true, (DateOnly?)null, (string?)null), (property.IsActive, property.DeactivatedOn, property.DeactivationReason));
  }
}

public class CustomFieldScopeRuleTests
{
  private static AttributeDefinition Field(AttributeDataType type = AttributeDataType.Dropdown, string? options = "North,South") =>
      AttributeDefinition.Create(AttributeDefinitionId.New(), Master<AttributeGroup>("GENERAL"), MasterCode.Of("PLOT_FACING"), Name.Of("Plot facing"),
        type, false, options, null, 0);

  [Fact]
  public void A_field_applies_only_to_the_property_types_and_classifications_it_names()
  {
    var field = Field();
    Guid residential = Guid.NewGuid(), commercial = Guid.NewGuid(), gda = Guid.NewGuid();
    Assert.True(field.AppliesTo(residential, gda));

    field.SetScope(new[] { residential }, null);
    Assert.True(field.AppliesTo(residential, gda));
    Assert.False(field.AppliesTo(commercial, gda));
  }

  [Fact]
  public void Once_values_exist_the_kind_of_value_is_fixed_and_options_in_use_stay()
  {
    var field = Field();
    var group = Master<AttributeGroup>("GENERAL");

    Assert.Contains("its kind of value cannot change", Message(() => field.Update(group, Name.Of("Plot facing"), AttributeDataType.Text, false, null, null, 0, new[] { "North" })));
    Assert.Contains("'North'", Message(() => field.Update(group, Name.Of("Plot facing"), AttributeDataType.Dropdown, false, "South,East", null, 0, new[] { "North" })));

    field.Update(group, Name.Of("Plot facing"), AttributeDataType.Dropdown, false, "North,South,East", null, 0, new[] { "North" });
    Assert.Equal(3, field.Options.Count);
  }
}
