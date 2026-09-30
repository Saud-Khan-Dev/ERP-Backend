using static Fixture;

public class BoundaryTests
{
  private static readonly GeoPoint[] Square =
  {
    GeoPoint.Of(34.0781234m, 73.3871234m), GeoPoint.Of(34.0782234m, 73.3872234m),
    GeoPoint.Of(34.0781234m, 73.3873234m), GeoPoint.Of(34.0780234m, 73.3872234m)
  };

  [Fact]
  public void Boundary_stores_slope_as_a_percentage_and_ordered_points()
  {
    var boundary = PropertyBoundary.Record(BoundaryId.New(), Property(), BoundaryType.Original, null, null, 12.5m, null, Square);

    Assert.Equal(12.5m, boundary.SlopePercentage);
    Assert.Equal(new[] { 1, 2, 3, 4 }, boundary.Points.Select(p => p.SequenceNo));
    Assert.True(boundary.IsCurrent);
  }

  [Theory]
  [InlineData(-0.01)]
  [InlineData(10000)]
  public void Slope_outside_decimal_8_4_percentage_range_is_refused(decimal slope)
  {
    Assert.Throws<DomainException>(() => PropertyBoundary.Record(BoundaryId.New(), Property(), BoundaryType.Original, null, null, slope, null, Square));
  }

  [Fact]
  public void Polygon_needs_three_distinct_points()
  {
    Assert.Throws<DomainException>(() => PropertyBoundary.Record(BoundaryId.New(), Property(), BoundaryType.Original, null, null, null, null,
      new[] { Square[0], Square[1], Square[0] }));
  }

  [Theory]
  [InlineData(91, 0)]
  [InlineData(0, 181)]
  public void Coordinates_must_be_on_earth(decimal latitude, decimal longitude)
  {
    Assert.Throws<DomainException>(() => GeoPoint.Of(latitude, longitude));
  }
}

public class EncroachmentTests
{
  private static PropertyEncroachment Encroachment() => PropertyEncroachment.Record(EncroachmentId.New(), Property(), BusinessCode.Of("ENC-00001"),
    5, Unit("MARLA", 272.25m), Master<EncroachmentStatus>("ACTIVE"), D("2026-06-01"),
    new PropertyEncroachment.Details("Kiosk", null, null, null, null), null);

  [Fact]
  public void Encroached_area_is_kept_in_square_feet()
  {
    Assert.Equal(1361.25m, Encroachment().EncroachmentAreaBase);
  }

  [Fact]
  public void Resolution_type_decides_the_status()
  {
    var encroachment = Encroachment();

    Assert.Throws<DomainException>(() =>
      encroachment.Resolve(Master<EncroachmentStatus>("RESOLVED"), D("2026-07-01"), EncroachmentResolution.Regularized, null));

    encroachment.Resolve(Master<EncroachmentStatus>("REGULARIZED"), D("2026-07-01"), EncroachmentResolution.Regularized, null);
    Assert.False(encroachment.IsUnresolved);
  }

  [Fact]
  public void Encroached_polygon_is_captured_once()
  {
    var encroachment = Encroachment();
    var points = new[] { GeoPoint.Of(34, 73), GeoPoint.Of(34.001m, 73), GeoPoint.Of(34, 73.001m) };
    encroachment.SetBoundary(points);

    Assert.Throws<DomainException>(() => encroachment.SetBoundary(points));
  }
}

public class AppealTests
{
  private static PropertyAppeal File(string orderDate, string? received, string appealDate, out IReadOnlyList<string> warnings) =>
      PropertyAppeal.File(AppealId.New(), Property(), BusinessCode.Of("APL-00001"),
        new PropertyAppeal.Filing(Owner("Appellant"), "DG/7", D(orderDate), received is null ? null : D(received), null, null, D(appealDate), null, null, null),
        out warnings);

  [Fact]
  public void Rule6_decision_due_is_appeal_date_plus_120_days()
  {
    var appeal = File("2031-02-10", "2031-02-12", "2031-03-01", out var warnings);

    Assert.Equal(D("2031-06-29"), appeal.DecisionDueDate);
    Assert.Empty(warnings);
    Assert.Equal("Chief Secretary, Khyber Pakhtunkhwa", appeal.AppellateAuthority);
  }

  [Fact]
  public void Rule6_filing_more_than_30_days_after_receipt_warns()
  {
    File("2031-02-10", "2031-02-12", "2031-03-25", out var warnings);

    Assert.Single(warnings);
    Assert.Contains("41 days", warnings[0]);
  }

  [Fact]
  public void Decision_is_final()
  {
    var appeal = File("2031-02-10", null, "2031-02-20", out _);
    appeal.Decide(D("2031-05-01"), AppealOutcome.Dismissed, null);

    Assert.Throws<DomainException>(() => appeal.Withdraw(null));
  }
}

public class BuildingPlanTests
{
  private static readonly BuildingPlanStatus Submitted = Master<BuildingPlanStatus>("SUBMITTED");
  private static readonly BuildingPlanStatus Approved = Master<BuildingPlanStatus>("APPROVED");
  private static readonly BuildingPlanStatus Revised = Master<BuildingPlanStatus>("REVISED");

  private static BuildingPlan.Details Details() => new(Master<BuildingPlanType>("SITE_PLAN"), null, D("2026-05-01"), null, null, 2, null, null);

  [Fact]
  public void Revision_keeps_the_plan_number_and_supersedes_the_previous_row()
  {
    var plan = BuildingPlan.Submit(BuildingPlanId.New(), Property(), BusinessCode.Of("BP-00001"), Submitted, Details());
    plan.Approve(Submitted, Approved, D("2026-05-20"), "officer", null, null);

    Assert.Throws<DomainException>(() => plan.Update(Approved, Details()));

    var revision = plan.Revise(Approved, Revised, Submitted, BuildingPlanId.New(), Details());

    Assert.Equal((plan.PlanNo, 1, plan.Id), (revision.PlanNo, revision.RevisionNo, revision.SupersedesPlanId!));
    Assert.Equal(Revised.Id, plan.BuildingPlanStatusId);
  }

  [Fact]
  public void Covered_area_needs_its_unit()
  {
    var details = Details() with { CoveredArea = 3000 };

    Assert.Throws<DomainException>(() => BuildingPlan.Submit(BuildingPlanId.New(), Property(), BusinessCode.Of("BP-00001"), Submitted, details));
  }
}

public class LitigationTests
{
  [Fact]
  public void Act_s30_criminal_complaint_must_record_the_filing_officer()
  {
    var complaint = Master<LitigationType>("CRIMINAL_COMPLAINT");

    Assert.Throws<DomainException>(() => PropertyLitigation.File(LitigationId.New(), Property(), "CC-1", "District & Sessions Judge Abbottabad",
      Master<LitigationStatus>("PENDING"), new PropertyLitigation.Details("Complaint", complaint, null, null, null, null, null),
      new PropertyLitigation.Related(null, null, null, null), Array.Empty<PropertyLitigation.PartyInput>()));
  }

  [Fact]
  public void Hearing_sets_the_next_hearing_and_decision_closes_the_case()
  {
    var pending = Master<LitigationStatus>("PENDING");
    var decided = Master<LitigationStatus>("DECIDED");
    var litigation = PropertyLitigation.File(LitigationId.New(), Property(), "CS-1", "Civil Judge", pending,
      new PropertyLitigation.Details("GDA vs X", Master<LitigationType>("CIVIL_SUIT"), D("2026-06-05"), GdaRole.Plaintiff, null, null, null),
      new PropertyLitigation.Related(null, null, null, null), Array.Empty<PropertyLitigation.PartyInput>());

    litigation.RecordHearing(pending, D("2026-07-10"), null, "Adjourned", D("2026-08-10"), null);
    Assert.Equal(D("2026-08-10"), litigation.NextHearingDate);

    litigation.Decide(pending, decided, D("2026-09-01"), "Decree for GDA");
    Assert.Null(litigation.NextHearingDate);
    Assert.Throws<DomainException>(() => litigation.RecordHearing(decided, D("2026-09-10"), null, null, null, null));
  }
}

public class DocumentTests
{
  private static readonly PropertyDocument.StoredFile File = new("a.pdf", "/property-documents/PROP-00001/notices/a.pdf", "application/pdf", 10, new string('a', 64));

  [Fact]
  public void Document_always_belongs_to_a_property()
  {
    Assert.Throws<ArgumentNullException>(() => PropertyDocument.Create(DocumentId.New(), null!, Master<DocumentType>("NOTICE_LETTER"),
      DocumentEntityType.Owner, Guid.NewGuid(), "cnic.pdf", File, new PropertyDocument.Details(null, null, null, null, false), Guid.NewGuid(), DateTime.UtcNow));
  }

  [Fact]
  public void New_version_supersedes_the_old_one()
  {
    var property = Property();
    var document = PropertyDocument.Create(DocumentId.New(), property.Id, Master<DocumentType>("NOTICE_LETTER"), DocumentEntityType.Property,
      property.Id.Value, "notice.pdf", File, new PropertyDocument.Details("Notice", null, null, null, false), Guid.NewGuid(), DateTime.UtcNow);

    var version = document.NewVersion(DocumentId.New(), "notice-v2.pdf", File, Guid.NewGuid(), DateTime.UtcNow);

    Assert.Equal((2, document.Id, "Notice"), (version.VersionNo, version.SupersedesDocumentId!, version.Title));
  }
}
