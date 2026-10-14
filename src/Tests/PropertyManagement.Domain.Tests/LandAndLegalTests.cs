using static Fixture;

// Rules of the land records (status, measurements, boundaries, regularization, building plans) and the legal
// side (encroachments, court cases, appeals, agreement violations, auctions, leases, rentals, contracts).

public class PropertyStatusRuleTests
{
  private static readonly DateOnly Today = D("2026-10-13");

  [Fact]
  public void Status_cannot_start_in_the_future()
  {
    var property = Property();
    var opening = property.OpenStatusHistory(D("2026-01-01"), null, Today);

    var error = Assert.Throws<DomainException>(() =>
      property.ChangeStatus(Master<PropertyStatus>("VACANT"), opening, Today.AddDays(1), null, null, Today));

    Assert.Contains("A status cannot start in the future.", error.Message);
    Assert.True(property.ChangeStatus(Master<PropertyStatus>("VACANT"), opening, Today, null, null, Today).IsOpen);
  }

  [Fact]
  public void Opening_status_cannot_start_in_the_future()
  {
    Assert.Throws<DomainException>(() => Property().OpenStatusHistory(Today.AddDays(1), null, Today));
  }

  [Fact]
  public void Inactive_property_tells_the_clerk_to_reactivate_it()
  {
    var property = Property();
    property.Deactivate();

    var error = Assert.Throws<DomainException>(property.EnsureActive);
    Assert.Contains("Reactivate it", error.Message);
  }
}

public class MeasurementRuleTests
{
  private static readonly DateOnly Today = D("2026-10-13");
  private static readonly MeasurementUnit Sqft = Unit("SQFT", 1, true);
  private readonly Property _property = Property();

  private PropertyMeasurement Measure(string? on, IReadOnlyCollection<PropertyMeasurement> current, out IReadOnlyList<string> warnings, decimal total = 1000, decimal builtUp = 500) =>
      PropertyMeasurement.Record(PropertyMeasurementId.New(), _property, Sqft, total, builtUp, on is null ? null : D(on), null, null, Today, current, out warnings);

  [Fact]
  public void Older_survey_is_kept_as_history_and_the_current_one_stays()
  {
    var current = Measure("2026-06-01", Array.Empty<PropertyMeasurement>(), out _);
    var older = Measure("2025-01-01", new[] { current }, out var warnings);

    Assert.False(older.IsCurrent);
    Assert.True(current.IsCurrent);
    Assert.Contains("kept as history", Assert.Single(warnings));
  }

  [Theory]
  [InlineData("2026-06-01")]
  [InlineData("2026-09-01")]
  public void Same_day_or_newer_survey_supersedes_the_current_one(string measuredOn)
  {
    var current = Measure("2026-06-01", Array.Empty<PropertyMeasurement>(), out _);
    var latest = Measure(measuredOn, new[] { current }, out var warnings);

    Assert.True(latest.IsCurrent);
    Assert.False(current.IsCurrent);
    Assert.Empty(warnings);
  }

  [Fact]
  public void Built_up_area_cannot_exceed_the_total_area()
  {
    var error = Assert.Throws<DomainException>(() => Measure(null, Array.Empty<PropertyMeasurement>(), out _, total: 1000, builtUp: 1000.01m));
    Assert.Contains("Built-up area cannot be more than the total area.", error.Message);
  }

  [Fact]
  public void Measured_date_cannot_be_in_the_future()
  {
    Assert.Throws<DomainException>(() => Measure("2026-10-14", Array.Empty<PropertyMeasurement>(), out _));
  }
}

public class BoundarySlopeTests
{
  private static readonly DateOnly Today = D("2026-10-13");

  private static readonly GeoPoint[] Triangle = { GeoPoint.Of(34, 73), GeoPoint.Of(34.001m, 73), GeoPoint.Of(34, 73.001m) };

  private static PropertyBoundary Survey(decimal? slope, out IReadOnlyList<string> warnings, Property? property = null, string? on = null,
      IReadOnlyCollection<PropertyBoundary>? current = null) =>
      PropertyBoundary.Record(BoundaryId.New(), property ?? Property(), BoundaryType.Original, on is null ? null : D(on), null, slope, null, Triangle,
        Today, current ?? Array.Empty<PropertyBoundary>(), out warnings);

  [Fact]
  public void Slope_of_45_degrees_is_within_the_limit()
  {
    var boundary = Survey(45, out var warnings);

    Assert.Equal(45m, boundary.SlopeDegrees);
    Assert.False(boundary.IsSteep);
    Assert.Empty(warnings);
  }

  [Theory]
  [InlineData(45.01)]
  [InlineData(90)]
  public void Slope_above_45_degrees_is_recorded_with_a_warning(decimal slope)
  {
    var boundary = Survey(slope, out var warnings);

    Assert.True(boundary.IsSteep);
    Assert.Equal(slope, boundary.SlopeDegrees);
    Assert.Contains("above the 45° limit of the Building & Construction Regulations 2026", Assert.Single(warnings));
  }

  [Fact]
  public void Steep_slope_warning_names_the_slope()
  {
    Survey(52, out var warnings);

    Assert.Equal("Slope 52° is above the 45° limit of the Building & Construction Regulations 2026 - the survey is recorded; check before approving construction.",
      Assert.Single(warnings));
  }

  [Theory]
  [InlineData(91)]
  [InlineData(90.01)]
  [InlineData(-0.01)]
  public void Slope_outside_0_to_90_degrees_is_refused(decimal slope)
  {
    var error = Assert.Throws<DomainException>(() => Survey(slope, out _));
    Assert.Contains("Slope must be between 0 and 90 degrees.", error.Message);
  }

  [Fact]
  public void Older_survey_is_history_and_a_newer_one_supersedes()
  {
    var property = Property();
    var current = Survey(10, out _, property, "2026-05-01");

    var older = Survey(10, out var warnings, property, "2026-01-01", new[] { current });
    Assert.False(older.IsCurrent);
    Assert.True(current.IsCurrent);
    Assert.Contains("kept as history", Assert.Single(warnings));

    var newer = Survey(10, out _, property, "2026-07-01", new[] { current });
    Assert.True(newer.IsCurrent);
    Assert.False(current.IsCurrent);
  }

  [Fact]
  public void Survey_date_cannot_be_in_the_future()
  {
    Assert.Throws<DomainException>(() => Survey(10, out _, on: "2026-10-14"));
  }
}

public class RegularizationRuleTests
{
  [Fact]
  public void Withdrawn_case_cannot_be_withdrawn_again_or_changed()
  {
    var regularization = PropertyAreaRegularization.Open(AreaRegularizationId.New(), Property(), Unit("SQFT", 1, true), 500,
      RegularizationStatus.Applied, D("2026-01-01"), null, null, null, null, null);

    regularization.Deactivate();

    Assert.Contains("This regularization case is already withdrawn.", Assert.Throws<DomainException>(regularization.Deactivate).Message);
    Assert.Throws<DomainException>(() => regularization.UpdateCase(RegularizationStatus.Pending, null, null, null, null, null, null));
  }
}

public class BuildingPlanWithdrawalTests
{
  private static readonly BuildingPlanStatus Submitted = Master<BuildingPlanStatus>("SUBMITTED");
  private static readonly BuildingPlanStatus Withdrawn = Master<BuildingPlanStatus>("WITHDRAWN");
  private static readonly BuildingPlanStatus Approved = Master<BuildingPlanStatus>("APPROVED");

  private static BuildingPlan.Details Details() => new(Master<BuildingPlanType>("SITE_PLAN"), null, D("2026-05-01"), null, null, 2, null, null);

  [Fact]
  public void Withdrawn_plan_is_closed()
  {
    var plan = BuildingPlan.Submit(BuildingPlanId.New(), Property(), BusinessCode.Of("BP-00001"), Submitted, Details());
    plan.ChangeStatus(Submitted, Withdrawn);

    Assert.Throws<DomainException>(() => plan.Update(Withdrawn, Details()));
    Assert.Throws<DomainException>(() => plan.Approve(Withdrawn, Approved, D("2026-06-01"), "officer", null, null));
    Assert.Throws<DomainException>(() => plan.ChangeStatus(Withdrawn, Master<BuildingPlanStatus>("UNDER_REVIEW")));
    Assert.True(BuildingPlan.IsClosed(Withdrawn));
  }
}

public class EncroachmentRuleTests
{
  private static readonly DateOnly Today = D("2026-10-13");
  private static readonly EncroachmentStatus Active = Master<EncroachmentStatus>("ACTIVE");
  private static readonly EncroachmentStatus UnderNotice = Master<EncroachmentStatus>("UNDER_NOTICE");
  private static readonly EncroachmentStatus UnderLitigation = Master<EncroachmentStatus>("UNDER_LITIGATION");

  private static PropertyEncroachment Record(string? inPlaceSince = null, PropertyEncroachment.Notice? notice = null, string detected = "2026-06-01") =>
      PropertyEncroachment.Record(EncroachmentId.New(), Property(), BusinessCode.Of("ENC-00001"), 5, Unit("MARLA", 272.25m), Active, D(detected),
        new PropertyEncroachment.Details("Kiosk", null, inPlaceSince is null ? null : D(inPlaceSince), null, null), null, Today, notice, UnderNotice);

  [Fact]
  public void In_place_since_must_be_on_or_before_detection()
  {
    var error = Assert.Throws<DomainException>(() => Record(inPlaceSince: "2026-06-02"));
    Assert.Contains("In place since cannot be after the detection date.", error.Message);

    Assert.Equal(D("2026-06-01"), Record(inPlaceSince: "2026-06-01").EffectiveDate);
    Assert.Equal(D("2025-01-01"), Record(inPlaceSince: "2025-01-01").EffectiveDate);
  }

  [Fact]
  public void Notice_date_without_notice_no_is_refused()
  {
    Assert.Throws<DomainException>(() => Record(notice: new PropertyEncroachment.Notice(null, D("2026-06-05"))));
  }

  [Fact]
  public void Notice_given_with_the_case_puts_it_under_notice()
  {
    var encroachment = Record(notice: new PropertyEncroachment.Notice("GDA/N/1", null));

    Assert.Equal(("GDA/N/1", D("2026-06-01")), (encroachment.NoticeNo, encroachment.NoticeDate!.Value));
    Assert.Equal(UnderNotice.Id, encroachment.EncroachmentStatusId);
  }

  [Fact]
  public void Issuing_a_notice_moves_an_active_case_under_notice()
  {
    var encroachment = Record();
    encroachment.IssueNotice(Active, UnderNotice, "GDA/N/2", D("2026-06-10"), Today);

    Assert.Equal(UnderNotice.Id, encroachment.EncroachmentStatusId);
  }

  [Fact]
  public void Notice_on_a_case_under_litigation_keeps_its_status()
  {
    var encroachment = Record();
    encroachment.MarkUnderLitigation(UnderLitigation);
    encroachment.IssueNotice(UnderLitigation, UnderNotice, "GDA/N/3", D("2026-06-10"), Today);

    Assert.Equal(UnderLitigation.Id, encroachment.EncroachmentStatusId);
  }

  [Fact]
  public void Notice_cannot_be_dated_in_the_future_or_before_detection()
  {
    var encroachment = Record();

    Assert.Throws<DomainException>(() => encroachment.IssueNotice(Active, UnderNotice, "N", Today.AddDays(1), Today));
    Assert.Throws<DomainException>(() => encroachment.IssueNotice(Active, UnderNotice, "N", D("2026-05-31"), Today));
  }

  [Fact]
  public void Detection_date_cannot_be_in_the_future()
  {
    Assert.Throws<DomainException>(() => Record(detected: "2026-10-14"));
  }

  [Fact]
  public void Court_case_puts_only_an_unresolved_encroachment_under_litigation()
  {
    var open = Record();
    open.MarkUnderLitigation(UnderLitigation);
    Assert.Equal(UnderLitigation.Id, open.EncroachmentStatusId);

    var resolved = Record();
    var resolvedStatus = Master<EncroachmentStatus>("RESOLVED");
    resolved.Resolve(resolvedStatus, D("2026-07-01"), EncroachmentResolution.Removed, null);
    resolved.MarkUnderLitigation(UnderLitigation);
    Assert.Equal(resolvedStatus.Id, resolved.EncroachmentStatusId);
  }
}

public class CourtCaseRuleTests
{
  private static readonly LitigationStatus Pending = Master<LitigationStatus>("PENDING");
  private static readonly LitigationStatus Decided = Master<LitigationStatus>("DECIDED");
  private static readonly LitigationStatus Appealed = Master<LitigationStatus>("APPEALED");
  private static readonly LitigationType Civil = Master<LitigationType>("CIVIL_SUIT");
  private static readonly LitigationType Criminal = Master<LitigationType>("CRIMINAL_COMPLAINT");
  private static readonly PropertyLitigation.Related None = new(null, null, null, null);

  private readonly Property _property = Property();

  private PropertyLitigation File(string caseNo = "CS-1", LitigationType? type = null, Guid? officer = null, PropertyLitigation.Related? related = null,
      LitigationStatus? status = null) =>
      PropertyLitigation.File(LitigationId.New(), _property, caseNo, "Civil Judge Abbottabad", status ?? Pending,
        new PropertyLitigation.Details("GDA vs X", type ?? Civil, D("2026-06-05"), GdaRole.Plaintiff, null, null),
        related ?? None, Array.Empty<PropertyLitigation.PartyInput>(), officer);

  [Fact]
  public void Closed_set_is_decided_appealed_withdrawn_settled_closed()
  {
    Assert.Equal(new[] { "DECIDED", "APPEALED", "WITHDRAWN", "SETTLED", "CLOSED" }, PropertyLitigation.ClosedStatusCodes);
  }

  [Theory]
  [InlineData("WITHDRAWN")]
  [InlineData("SETTLED")]
  [InlineData("CLOSED")]
  public void Closed_case_takes_no_hearing_party_decision_edit_or_status(string code)
  {
    var closed = Master<LitigationStatus>(code);
    var litigation = File();
    litigation.RecordHearing(Pending, D("2026-07-01"), null, null, D("2026-08-01"), null);
    litigation.ChangeStatus(Pending, closed);

    Assert.Null(litigation.NextHearingDate);
    Assert.Throws<DomainException>(() => litigation.RecordHearing(closed, D("2026-08-01"), null, null, null, null));
    Assert.Throws<DomainException>(() => litigation.AddParty(closed, new PropertyLitigation.PartyInput("Y", null, LitigationPartyRole.Defendant, null, null)));
    Assert.Throws<DomainException>(() => litigation.Decide(closed, Decided, D("2026-09-01"), "Decree"));
    Assert.Throws<DomainException>(() => litigation.Update(closed, new PropertyLitigation.Details("New title", Civil, null, null, null, null)));
    Assert.Throws<DomainException>(() => litigation.ChangeStatus(closed, Pending));
  }

  [Fact]
  public void New_case_cannot_start_closed()
  {
    Assert.Throws<DomainException>(() => File(status: Master<LitigationStatus>("WITHDRAWN")));
  }

  [Fact]
  public void Criminal_complaint_records_its_filing_officer_and_edits_never_replace_it()
  {
    var officer = Guid.NewGuid();
    var complaint = File(type: Criminal, officer: officer);
    Assert.Equal(officer, complaint.FiledByOfficerId);

    Assert.False(complaint.BecomesCriminalComplaint(Criminal));
    complaint.Update(Pending, new PropertyLitigation.Details("Corrected title", Criminal, null, null, null, null), officerId: Guid.NewGuid());
    Assert.Equal(officer, complaint.FiledByOfficerId);
  }

  [Fact]
  public void Civil_case_has_no_filing_officer_until_it_becomes_a_complaint()
  {
    var suit = File(officer: Guid.NewGuid());
    Assert.Null(suit.FiledByOfficerId);

    Assert.True(suit.BecomesCriminalComplaint(Criminal));
    Assert.Throws<DomainException>(() => suit.Update(Pending, new PropertyLitigation.Details("GDA vs X", Criminal, null, null, null, null)));

    var officer = Guid.NewGuid();
    suit.Update(Pending, new PropertyLitigation.Details("GDA vs X", Criminal, null, null, null, null), officer);
    Assert.Equal(officer, suit.FiledByOfficerId);
  }

  [Fact]
  public void Appeal_as_new_case_needs_a_decided_lower_court_case()
  {
    var lower = File("CS-1");

    var pending = Assert.Throws<DomainException>(() => File("CA-1", related: new PropertyLitigation.Related(null, null, null, lower, Pending)));
    Assert.Contains("only a decided case can be appealed", pending.Message);

    lower.Decide(Pending, Decided, D("2026-09-01"), "Suit dismissed");
    var appeal = File("CA-1", related: new PropertyLitigation.Related(null, null, null, lower, Decided));
    lower.MarkAppealed(Decided, Appealed, "Peshawar High Court");

    Assert.Equal(lower.Id, appeal.ParentLitigationId);
    Assert.Equal(Appealed.Id, lower.LitigationStatusId);
    Assert.Throws<DomainException>(() => File("CA-2", related: new PropertyLitigation.Related(null, null, null, lower, Appealed)));
  }
}

public class AppealRuleTests
{
  private readonly Property _property = Property();
  private readonly PropertyOwner _appellant = Owner("Appellant");

  private PropertyAppeal File(string orderRef, IReadOnlyCollection<PropertyAppeal> others, Guid? violationId = null) =>
      PropertyAppeal.File(AppealId.New(), _property, BusinessCode.Of("APL-00001"),
        new PropertyAppeal.Filing(_appellant, orderRef, D("2026-03-01"), null,
          violationId is null ? null : AppealOrderSource.AgreementViolation, violationId, D("2026-03-10"), null, null, null),
        others, out _);

  [Fact]
  public void Second_appeal_against_an_open_or_decided_order_is_refused()
  {
    var first = File("DG/7", Array.Empty<PropertyAppeal>());

    Assert.Contains("still pending", Assert.Throws<DomainException>(() => File("dg/7", new[] { first })).Message);

    first.Decide(D("2026-05-01"), AppealOutcome.Dismissed, null);
    Assert.Contains("decision is final", Assert.Throws<DomainException>(() => File("DG/7", new[] { first })).Message);
  }

  [Fact]
  public void Withdrawn_appeal_does_not_block_a_new_one()
  {
    var first = File("DG/7", Array.Empty<PropertyAppeal>());
    first.Withdraw("filed in error");

    Assert.Equal(AppealStatus.Filed, File("DG/7", new[] { first }).AppealStatus);
  }

  [Fact]
  public void Same_record_is_the_same_order_even_under_another_reference()
  {
    var violationId = Guid.NewGuid();
    var first = File("DG/7", Array.Empty<PropertyAppeal>(), violationId);

    Assert.Throws<DomainException>(() => File("DG/7-A", new[] { first }, violationId));
    Assert.Equal(AppealStatus.Filed, File("DG/7", new[] { first }, Guid.NewGuid()).AppealStatus);
  }

  [Fact]
  public void Appealed_record_cannot_be_changed_on_update()
  {
    var appeal = File("DG/7", Array.Empty<PropertyAppeal>(), Guid.NewGuid());

    Assert.Throws<DomainException>(() => appeal.Update(
      new PropertyAppeal.Filing(_appellant, "DG/7", D("2026-03-01"), null, AppealOrderSource.AgreementViolation, Guid.NewGuid(), D("2026-03-10"), null, null, null),
      Array.Empty<PropertyAppeal>()));
  }
}

public class ViolationRuleTests
{
  private static readonly DateOnly Today = D("2026-10-13");
  private static readonly AgreementType LeaseAgreement = Master<AgreementType>("LEASE");
  private static readonly LeaseStatus Active = Master<LeaseStatus>("ACTIVE");
  private static readonly LeaseStatus Cancelled = Master<LeaseStatus>("CANCELLED");

  private readonly Property _property = Property();
  private readonly PropertyOwner _lessee = Owner("Lessee");
  private readonly PropertyLease _lease;

  public ViolationRuleTests() =>
    _lease = PropertyLease.Create(LeaseId.New(), _property, _lessee, BusinessCode.Of("LSE-00001"), Active,
      new PropertyLease.Terms(Master<LeaseType>("COMMERCIAL"), D("2026-01-01"), D("2030-12-31"), null, null, null, null, null, null, null, false, null));

  private AgreementViolation Record(string date, IReadOnlyCollection<AgreementViolation> others, out IReadOnlyList<string> warnings,
      string? noticeNo = null, string? noticeDate = null, string? deadline = null) =>
      AgreementViolation.Record(ViolationId.New(), _property, LeaseAgreement, new AgreementViolation.Agreement(_lease, null, null), _lessee,
        D(date), "Default in lease money", new AgreementViolation.Notice(noticeNo, noticeDate is null ? null : D(noticeDate), deadline is null ? null : D(deadline)),
        others, agreementInForce: true, null, Today, out warnings);

  private AgreementViolation Record(string date, params AgreementViolation[] others) => Record(date, others, out _);

  [Fact]
  public void Occurrence_counts_only_violations_dated_before()
  {
    var march = Record("2026-03-01");
    var april = Record("2026-04-01", march);
    var february = Record("2026-02-01", march, april);

    Assert.Equal((1, 2, 1), (march.OccurrenceNo, april.OccurrenceNo, february.OccurrenceNo));
  }

  [Fact]
  public void Rule5_third_violation_counts_only_inside_the_notice_period()
  {
    // the notice on the first violation runs 2026-03-01 .. 2026-03-31
    var first = Record("2026-02-01", Array.Empty<AgreementViolation>(), out _, "N-1", "2026-03-01", "2026-03-31");
    var second = Record("2026-02-10", first);

    var beforeNotice = Record("2026-02-20", first, second);
    Assert.Equal(3, beforeNotice.OccurrenceNo);
    Assert.False(beforeNotice.LedToCancellation);

    var insideNotice = Record("2026-03-15", first, second);
    Assert.True(insideNotice.LedToCancellation);
    Assert.Equal(ViolationStatus.Cancelled, insideNotice.ViolationStatus);
  }

  [Fact]
  public void Violation_is_dated_within_the_agreement_and_not_in_the_future()
  {
    Assert.Contains("LSE-00001 started (2026-01-01)", Assert.Throws<DomainException>(() => Record("2025-12-31")).Message);
    Assert.Throws<DomainException>(() => Record("2026-10-14"));
  }

  [Fact]
  public void Notice_without_comply_by_date_warns()
  {
    Record("2026-02-01", Array.Empty<AgreementViolation>(), out var warnings, "N-1", "2026-02-02");
    Assert.Equal(AgreementViolation.NoDeadlineWarning, Assert.Single(warnings));

    var later = Record("2026-03-01");
    Assert.Equal(AgreementViolation.NoDeadlineWarning, Assert.Single(later.IssueNotice(new AgreementViolation.Notice("N-2", D("2026-03-02"), null), Today)));
  }

  [Fact]
  public void Notice_date_without_notice_no_is_refused()
  {
    Assert.Throws<DomainException>(() => Record("2026-02-01", Array.Empty<AgreementViolation>(), out _, null, "2026-02-02"));
  }

  [Fact]
  public void A_violation_carries_one_notice()
  {
    var violation = Record("2026-02-01", Array.Empty<AgreementViolation>(), out _, "N-1", "2026-02-02", "2026-03-01");

    var error = Assert.Throws<DomainException>(() => violation.IssueNotice(new AgreementViolation.Notice("N-2", D("2026-02-10"), D("2026-03-10")), Today));
    Assert.Contains("A notice was already issued on 2026-02-02", error.Message);
  }

  [Fact]
  public void Dismissed_or_withdrawn_appeal_restores_the_status_before_the_appeal()
  {
    var violation = Record("2026-02-01");
    violation.ImposeFine(50_000, Guid.NewGuid());
    violation.MarkAppealed();
    Assert.Equal((ViolationStatus.Appealed, ViolationStatus.Fined), (violation.ViolationStatus, violation.PreAppealStatus!.Value));

    var appeal = AppealAgainst(violation);
    appeal.Decide(D("2026-05-01"), AppealOutcome.Dismissed, null);

    Assert.False(violation.ApplyAppealOutcome(appeal));
    Assert.Equal(ViolationStatus.Fined, violation.ViolationStatus);
    Assert.Null(violation.PreAppealStatus);

    violation.MarkAppealed();
    var withdrawn = AppealAgainst(violation);
    withdrawn.Withdraw(null);
    violation.ApplyAppealOutcome(withdrawn);
    Assert.Equal(ViolationStatus.Fined, violation.ViolationStatus);
  }

  [Fact]
  public void Allowed_appeal_sets_the_violation_aside_and_waives_the_fine()
  {
    var violation = Record("2026-02-01");
    violation.ImposeFine(50_000, Guid.NewGuid());
    violation.MarkAppealed();

    var appeal = AppealAgainst(violation);
    appeal.Decide(D("2026-05-01"), AppealOutcome.Allowed, null);

    Assert.False(violation.ApplyAppealOutcome(appeal));
    Assert.Equal((ViolationStatus.Rectified, FineStatus.Waived), (violation.ViolationStatus, violation.FineStatus!.Value));
    Assert.Contains("Set aside on appeal APL-00009.", violation.Remarks);
  }

  [Fact]
  public void Allowed_appeal_against_the_cancelling_violation_reinstates_the_lease()
  {
    var first = Record("2026-02-01", Array.Empty<AgreementViolation>(), out _, "N-1", "2026-02-01", "2026-06-30");
    var second = Record("2026-03-01", first);
    var third = Record("2026-04-01", first, second);
    Assert.True(third.LedToCancellation);
    _lease.Cancel(Active, Cancelled, D("2026-04-01"), "s.28-A");

    // rectifying is refused whatever the status: only the appeal can set the cancellation aside
    Assert.Throws<DomainException>(() => third.Rectify(null));
    third.MarkAppealed();
    Assert.Throws<DomainException>(() => third.Rectify(null));

    var appeal = AppealAgainst(third);
    appeal.Decide(D("2026-05-01"), AppealOutcome.Allowed, null);
    Assert.True(third.ApplyAppealOutcome(appeal));

    _lease.Reinstate(Cancelled, Active, appeal.AppealNo.Value);
    Assert.Equal(Active.Id, _lease.LeaseStatusId);
    Assert.Null(_lease.TerminationDate);
    Assert.Null(_lease.TerminationReason);
    Assert.Contains("Reinstated by appeal APL-00009", _lease.Remarks);
  }

  [Fact]
  public void Dismissed_appeal_leaves_the_cancelling_violation_unrectifiable()
  {
    var first = Record("2026-02-01", Array.Empty<AgreementViolation>(), out _, "N-1", "2026-02-01", "2026-06-30");
    var third = Record("2026-04-01", first, Record("2026-03-01", first));
    third.MarkAppealed();

    var appeal = AppealAgainst(third);
    appeal.Decide(D("2026-05-01"), AppealOutcome.Dismissed, null);
    third.ApplyAppealOutcome(appeal);

    Assert.Equal(ViolationStatus.Cancelled, third.ViolationStatus);
    Assert.Throws<DomainException>(() => third.Rectify(null));
  }

  [Fact]
  public void Violation_already_under_appeal_cannot_be_appealed_again()
  {
    var violation = Record("2026-02-01");
    violation.MarkAppealed();

    Assert.Throws<DomainException>(violation.MarkAppealed);
  }

  private PropertyAppeal AppealAgainst(AgreementViolation violation) =>
      PropertyAppeal.File(AppealId.New(), _property, BusinessCode.Of("APL-00009"),
        new PropertyAppeal.Filing(_lessee, "DG/7", D("2026-04-02"), null, AppealOrderSource.AgreementViolation, violation.Id.Value, D("2026-04-10"), null, null, null),
        Array.Empty<PropertyAppeal>(), out _);
}

public class AuctionRuleTests
{
  private static readonly DateOnly Today = D("2026-10-13");
  private static readonly AuctionStatus Planned = Master<AuctionStatus>("PLANNED");
  private static readonly AuctionStatus Awarded = Master<AuctionStatus>("AWARDED");
  private static readonly AuctionStatus Unsuccessful = Master<AuctionStatus>("UNSUCCESSFUL");
  private static readonly AuctionStatus Cancelled = Master<AuctionStatus>("CANCELLED");
  private static readonly AuctionType ReAuctionType = Master<AuctionType>("RE_AUCTION");

  private readonly Property _property = Property();

  private PropertyAuction Auction(string? held = "2026-05-01") => PropertyAuction.Plan(AuctionId.New(), _property, BusinessCode.Of("AUC-00001"), Planned,
    new PropertyAuction.Details(Master<AuctionType>("OPEN_AUCTION"), null, held is null ? null : D(held), 1_000_000, "AC/1", "GDA Hall", null, null));

  [Theory]
  [InlineData("UNSUCCESSFUL")]
  [InlineData("CANCELLED")]
  public void Unsuccessful_and_cancelled_auctions_are_closed(string code)
  {
    var closed = Master<AuctionStatus>(code);
    var auction = Auction();
    if (code == "CANCELLED") auction.Cancel(Planned, closed, null);
    else auction.ChangeStatus(Planned, closed);

    Assert.Throws<DomainException>(() => auction.AddBid(closed, Owner("Late"), 2_000_000, null, null));
    Assert.Throws<DomainException>(() => auction.Award(closed, Awarded, null, Owner("W"), 2_000_000, D("2026-05-02"), null, Today));
    Assert.Throws<DomainException>(() => auction.Update(closed, new PropertyAuction.Details(Master<AuctionType>("OPEN_AUCTION"), null, null, null, null, null, null, null)));
    Assert.Throws<DomainException>(() => auction.ChangeStatus(closed, Master<AuctionStatus>("ANNOUNCED")));
  }

  [Fact]
  public void Successful_comes_only_through_award()
  {
    Assert.Throws<DomainException>(() => Auction().ChangeStatus(Planned, Master<AuctionStatus>("SUCCESSFUL")));
  }

  [Fact]
  public void Award_is_dated_from_the_auction_up_to_today()
  {
    var auction = Auction();

    Assert.Throws<DomainException>(() => auction.Award(Planned, Awarded, null, Owner("W"), 2_000_000, D("2026-04-30"), null, Today));
    Assert.Throws<DomainException>(() => auction.Award(Planned, Awarded, null, Owner("W"), 2_000_000, Today.AddDays(1), null, Today));
    Assert.Throws<DomainException>(() => Auction(held: null).Award(Planned, Awarded, null, Owner("W"), 2_000_000, D("2026-05-02"), null, Today));

    auction.Award(Planned, Awarded, null, Owner("W"), 2_000_000, D("2026-05-01"), null, Today);
    Assert.Equal(Awarded.Id, auction.AuctionStatusId);
  }

  [Fact]
  public void Re_auction_of_an_unsuccessful_auction_is_a_new_linked_planned_auction()
  {
    var auction = Auction();
    auction.ChangeStatus(Planned, Unsuccessful);

    var again = auction.ReAuction(Unsuccessful, _property, AuctionId.New(), BusinessCode.Of("AUC-00002"), Planned, ReAuctionType,
      D("2026-08-01"), null, "AC/2", existingReAuction: null);

    Assert.Equal((auction.Id, ReAuctionType.Id, Planned.Id), (again.PreviousAuctionId!, again.AuctionTypeId, again.AuctionStatusId));
    Assert.Equal((1_000_000m, "GDA Hall", "AC/2"), (again.BaseReservePrice!.Value, again.Venue!, again.AuctionCommitteeRef!));

    Assert.Throws<DomainException>(() => auction.ReAuction(Unsuccessful, _property, AuctionId.New(), BusinessCode.Of("AUC-00003"), Planned, ReAuctionType,
      D("2026-09-01"), null, null, existingReAuction: again));
  }

  [Fact]
  public void Only_an_unsuccessful_or_cancelled_auction_is_re_auctioned_and_after_the_original_date()
  {
    var open = Auction();
    Assert.Throws<DomainException>(() => open.ReAuction(Planned, _property, AuctionId.New(), BusinessCode.Of("AUC-00002"), Planned, ReAuctionType,
      D("2026-08-01"), null, null, null));

    var cancelled = Auction();
    cancelled.Cancel(Planned, Cancelled, "no bidders");
    Assert.Throws<DomainException>(() => cancelled.ReAuction(Cancelled, _property, AuctionId.New(), BusinessCode.Of("AUC-00002"), Planned, ReAuctionType,
      D("2026-05-01"), null, null, null));
    Assert.NotNull(cancelled.ReAuction(Cancelled, _property, AuctionId.New(), BusinessCode.Of("AUC-00002"), Planned, ReAuctionType,
      D("2026-05-02"), 900_000, null, null).PreviousAuctionId);
  }
}

public class AgreementEndingTests
{
  private static readonly DateOnly Today = D("2026-10-13");
  private static readonly LeaseStatus Draft = Master<LeaseStatus>("DRAFT");
  private static readonly LeaseStatus LeaseActive = Master<LeaseStatus>("ACTIVE");
  private static readonly LeaseStatus Expired = Master<LeaseStatus>("EXPIRED");
  private static readonly RentalStatus RentalActive = Master<RentalStatus>("ACTIVE");
  private static readonly RentalStatus Ended = Master<RentalStatus>("ENDED");
  private static readonly ContractStatus ContractActive = Master<ContractStatus>("ACTIVE");
  private static readonly ContractStatus ContractExpired = Master<ContractStatus>("EXPIRED");

  private static PropertyLease Lease(LeaseStatus status, string end) => PropertyLease.Create(LeaseId.New(), Property(), Owner("Lessee"), BusinessCode.Of("LSE-00001"), status,
    new PropertyLease.Terms(Master<LeaseType>("COMMERCIAL"), D("2020-01-01"), D(end), null, null, null, null, null, null, null, false, null));

  private static PropertyRental.Terms RentalTerms(string start, string? end = null) =>
    new(Master<RentalType>("SHOP"), D(start), end is null ? null : D(end), 25_000, RentFrequency.Monthly, null, null, null, null, null);

  private static PropertyOutsourcing Contract(string? end) => PropertyOutsourcing.Create(OutsourcingId.New(), Property(), Owner("Contractor"), BusinessCode.Of("CON-00001"),
    ContractActive, new PropertyOutsourcing.Terms(Master<OutsourcingType>("PARKING"), D("2024-01-01"), end is null ? null : D(end), null, null, null, null, null, null));

  [Fact]
  public void Lease_expires_only_after_its_end_date()
  {
    var lease = Lease(LeaseActive, "2026-10-13");

    Assert.Contains("LSE-00001 ends on 2026-10-13; it can be marked expired after that.",
      Assert.Throws<DomainException>(() => lease.Expire(LeaseActive, Expired, Today)).Message);

    lease.Expire(LeaseActive, Expired, Today.AddDays(1));
    Assert.Equal(Expired.Id, lease.LeaseStatusId);
  }

  [Fact]
  public void Draft_lease_cannot_expire()
  {
    var draft = Lease(Draft, "2021-01-01");

    Assert.Contains("draft", Assert.Throws<DomainException>(() => draft.Expire(Draft, Expired, Today)).Message);
  }

  [Fact]
  public void Renewed_rental_ends_the_day_before_the_renewal_starts()
  {
    var rental = PropertyRental.Create(RentalId.New(), Property(), Owner("Tenant"), BusinessCode.Of("RNT-00001"), RentalActive, RentalTerms("2025-01-01", "2026-12-31"));

    var renewal = rental.Renew(RentalActive, Ended, RentalActive, RentalId.New(), BusinessCode.Of("RNT-00002"), Owner("Tenant"), RentalTerms("2026-07-01"));

    Assert.Equal(D("2026-06-30"), rental.RentalEndDate);
    Assert.Equal(Ended.Id, rental.RentalStatusId);
    Assert.Equal(rental.Id, renewal.RenewedFromRentalId);
  }

  [Fact]
  public void Open_ended_rental_gets_its_end_date_on_renewal_and_renewal_starts_after_it()
  {
    var rental = PropertyRental.Create(RentalId.New(), Property(), Owner("Tenant"), BusinessCode.Of("RNT-00001"), RentalActive, RentalTerms("2025-01-01"));

    Assert.Throws<DomainException>(() =>
      rental.Renew(RentalActive, Ended, RentalActive, RentalId.New(), BusinessCode.Of("RNT-00002"), Owner("Tenant"), RentalTerms("2025-01-01")));

    rental.Renew(RentalActive, Ended, RentalActive, RentalId.New(), BusinessCode.Of("RNT-00002"), Owner("Tenant"), RentalTerms("2026-01-01"));
    Assert.Equal(D("2025-12-31"), rental.RentalEndDate);
  }

  [Fact]
  public void Cancelled_rental_is_reinstated_by_an_appeal()
  {
    var cancelled = Master<RentalStatus>("CANCELLED");
    var rental = PropertyRental.Create(RentalId.New(), Property(), Owner("Tenant"), BusinessCode.Of("RNT-00001"), RentalActive, RentalTerms("2025-01-01"));
    rental.Cancel(RentalActive, cancelled, D("2026-04-01"), "s.28-A");

    Assert.Throws<DomainException>(() => rental.Reinstate(RentalActive, RentalActive, "APL-00001"));
    rental.Reinstate(cancelled, RentalActive, "APL-00001");

    Assert.Equal((RentalActive.Id, (DateOnly?)null), (rental.RentalStatusId, rental.TerminationDate));
    Assert.Contains("Reinstated by appeal APL-00001", rental.Remarks);
  }

  [Fact]
  public void Contract_expires_only_after_its_end_date()
  {
    var contract = Contract("2026-10-13");
    Assert.Throws<DomainException>(() => contract.ChangeStatus(ContractActive, ContractExpired, Today));

    contract.ChangeStatus(ContractActive, ContractExpired, Today.AddDays(1));
    Assert.Equal(ContractExpired.Id, contract.ContractStatusId);
  }

  [Fact]
  public void Open_ended_contract_cannot_expire()
  {
    Assert.Contains("Terminate it instead", Assert.Throws<DomainException>(() => Contract(null).ChangeStatus(ContractActive, ContractExpired, Today)).Message);
  }
}
