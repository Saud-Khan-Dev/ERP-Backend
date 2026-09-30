using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

// =====================================================================
// 4. BOUNDARIES / GIS  and  13–15. ENCROACHMENT, LITIGATION & APPEALS, BUILDING PLANS
// Columns, sizes and indexes follow docs/gda_property_module.dbml.
// =====================================================================

/// property_boundary — one surveyed polygon, with the plot's slope as a percentage.
/// The ERD gives it created_at / created_by only.
public class PropertyBoundaryConfiguration : EntityConfiguration<PropertyBoundary, BoundaryId>
{
  protected override AuditColumns Audit => AuditColumns.Created;

  public override void Configure(EntityTypeBuilder<PropertyBoundary> builder)
  {
    base.Configure(builder);
    builder.ToTable("property_boundary");

    builder.HasKey(x => x.Id);
    builder.Property(x => x.Id).HasConversion(id => id.Value, value => BoundaryId.Of(value));
    builder.Property(x => x.PropertyId).HasConversion(id => id.Value, value => PropertyId.Of(value)).IsRequired();
    // ORIGINAL (default), REVISED, REGULARIZED
    builder.Property(x => x.BoundaryType).HasUpperSnakeEnum(30).IsRequired().HasDefaultValue(BoundaryType.Original);
    builder.Property(x => x.SurveySource).HasMaxLength(150);
    // ground slope as a percentage: 12.5000 = 12.50% slope (not degrees)
    builder.Property(x => x.SlopePercentage).HasPrecision(8, 4);
    builder.Property(x => x.IsCurrent).IsRequired().HasDefaultValue(true);

    builder.HasOne<Property>().WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);

    builder.HasMany(x => x.Points).WithOne().HasForeignKey(p => p.PropertyBoundaryId).OnDelete(DeleteBehavior.Restrict);
    builder.Navigation(x => x.Points).UsePropertyAccessMode(PropertyAccessMode.Field);
  }
}

/// boundary_point — latitude / longitude only; no elevation is stored. No audit columns in the ERD.
public class BoundaryPointConfiguration : EntityConfiguration<BoundaryPoint, BoundaryPointId>
{
  protected override AuditColumns Audit => AuditColumns.None;

  public override void Configure(EntityTypeBuilder<BoundaryPoint> builder)
  {
    base.Configure(builder);
    builder.ToTable("boundary_point");

    builder.HasKey(x => x.Id);
    builder.Property(x => x.Id).HasConversion(id => id.Value, value => BoundaryPointId.Of(value)).ValueGeneratedNever();
    builder.Property(x => x.PropertyBoundaryId).HasConversion(id => id.Value, value => BoundaryId.Of(value)).IsRequired();
    builder.Property(x => x.SequenceNo).IsRequired();
    builder.Property(x => x.Latitude).HasPrecision(10, 7).IsRequired();
    builder.Property(x => x.Longitude).HasPrecision(10, 7).IsRequired();

    builder.HasIndex(x => new { x.PropertyBoundaryId, x.SequenceNo }).IsUnique();
  }
}

/// property_encroachment
public class PropertyEncroachmentConfiguration : EntityConfiguration<PropertyEncroachment, EncroachmentId>
{
  public override void Configure(EntityTypeBuilder<PropertyEncroachment> builder)
  {
    base.Configure(builder);
    builder.ToTable("property_encroachment");

    builder.HasKey(x => x.Id);
    builder.Property(x => x.Id).HasConversion(id => id.Value, value => EncroachmentId.Of(value));
    builder.Property(x => x.PropertyId).HasConversion(id => id.Value, value => PropertyId.Of(value)).IsRequired();
    builder.Property(x => x.EncroachmentNo).HasBusinessCode(50).IsRequired();
    builder.Property(x => x.EncroachmentArea).HasPrecision(18, 4).IsRequired();
    builder.Property(x => x.MeasurementUnitId).HasMasterId().IsRequired();
    builder.Property(x => x.EncroachmentAreaBase).HasPrecision(18, 4);
    builder.Property(x => x.EncroachmentStatusId).HasMasterId().IsRequired();
    builder.Property(x => x.EncroacherName).HasMaxLength(200);
    builder.Property(x => x.EncroacherOwnerId).HasConversion(id => id!.Value, value => OwnerId.Of(value));
    builder.Property(x => x.DetectionDate).IsRequired();
    builder.Property(x => x.NoticeNo).HasMaxLength(50);
    // REMOVED, REGULARIZED, LITIGATED, OTHER
    builder.Property(x => x.ResolutionType).HasNullableUpperSnakeEnum(30);
    builder.Property(x => x.ResolutionReferenceNo).HasMaxLength(100);
    builder.Ignore(x => x.IsUnresolved);

    builder.HasIndex(x => x.EncroachmentNo).IsUnique();
    builder.HasIndex(x => new { x.PropertyId, x.EncroachmentStatusId });

    builder.HasOne<Property>().WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<MeasurementUnit>().WithMany().HasForeignKey(x => x.MeasurementUnitId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<EncroachmentStatus>().WithMany().HasForeignKey(x => x.EncroachmentStatusId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<PropertyOwner>().WithMany().HasForeignKey(x => x.EncroacherOwnerId).OnDelete(DeleteBehavior.Restrict);

    builder.HasMany(x => x.Points).WithOne().HasForeignKey(p => p.EncroachmentId).OnDelete(DeleteBehavior.Restrict);
    builder.Navigation(x => x.Points).UsePropertyAccessMode(PropertyAccessMode.Field);
  }
}

/// encroachment_boundary_point — its own polygon, never shared with the property boundary. No audit columns.
public class EncroachmentBoundaryPointConfiguration : EntityConfiguration<EncroachmentBoundaryPoint, EncroachmentPointId>
{
  protected override AuditColumns Audit => AuditColumns.None;

  public override void Configure(EntityTypeBuilder<EncroachmentBoundaryPoint> builder)
  {
    base.Configure(builder);
    builder.ToTable("encroachment_boundary_point");

    builder.HasKey(x => x.Id);
    builder.Property(x => x.Id).HasConversion(id => id.Value, value => EncroachmentPointId.Of(value)).ValueGeneratedNever();
    builder.Property(x => x.EncroachmentId).HasConversion(id => id.Value, value => EncroachmentId.Of(value)).IsRequired();
    builder.Property(x => x.SequenceNo).IsRequired();
    builder.Property(x => x.Latitude).HasPrecision(10, 7).IsRequired();
    builder.Property(x => x.Longitude).HasPrecision(10, 7).IsRequired();

    builder.HasIndex(x => new { x.EncroachmentId, x.SequenceNo }).IsUnique();
  }
}

/// property_litigation
public class PropertyLitigationConfiguration : EntityConfiguration<PropertyLitigation, LitigationId>
{
  public override void Configure(EntityTypeBuilder<PropertyLitigation> builder)
  {
    base.Configure(builder);
    builder.ToTable("property_litigation");

    builder.HasKey(x => x.Id);
    builder.Property(x => x.Id).HasConversion(id => id.Value, value => LitigationId.Of(value));
    builder.Property(x => x.PropertyId).HasConversion(id => id.Value, value => PropertyId.Of(value)).IsRequired();
    builder.Property(x => x.CaseNo).HasMaxLength(100).IsRequired();
    builder.Property(x => x.CaseTitle).HasMaxLength(300).IsRequired();
    builder.Property(x => x.CourtAuthority).HasMaxLength(200).IsRequired();
    builder.Property(x => x.LitigationTypeId).HasMasterId().IsRequired();
    builder.Property(x => x.LitigationStatusId).HasMasterId().IsRequired();
    // PLAINTIFF, DEFENDANT, COMPLAINANT, RESPONDENT
    builder.Property(x => x.GdaRole).HasNullableUpperSnakeEnum(20);
    // the authorized officer's Identity user id (the ERD's app_user lives in the Identity service)
    builder.Property(x => x.FiledByOfficerId);
    builder.Property(x => x.RelatedEncroachmentId).HasConversion(id => id!.Value, value => EncroachmentId.Of(value));
    builder.Property(x => x.RelatedAllotmentId).HasConversion(id => id!.Value, value => AllotmentId.Of(value));
    builder.Property(x => x.RelatedLeaseId).HasConversion(id => id!.Value, value => LeaseId.Of(value));
    builder.Property(x => x.AppealedTo).HasMaxLength(200);
    builder.Property(x => x.ParentLitigationId).HasConversion(id => id!.Value, value => LitigationId.Of(value));
    builder.Property(x => x.GdaCounsel).HasMaxLength(150);

    builder.HasIndex(x => new { x.PropertyId, x.LitigationStatusId });
    builder.HasIndex(x => new { x.CaseNo, x.CourtAuthority }).IsUnique();
    builder.HasIndex(x => x.NextHearingDate);

    builder.HasOne<Property>().WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<LitigationType>().WithMany().HasForeignKey(x => x.LitigationTypeId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<LitigationStatus>().WithMany().HasForeignKey(x => x.LitigationStatusId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<PropertyEncroachment>().WithMany().HasForeignKey(x => x.RelatedEncroachmentId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<PropertyAllotment>().WithMany().HasForeignKey(x => x.RelatedAllotmentId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<PropertyLease>().WithMany().HasForeignKey(x => x.RelatedLeaseId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<PropertyLitigation>().WithMany().HasForeignKey(x => x.ParentLitigationId).OnDelete(DeleteBehavior.Restrict);

    builder.HasMany(x => x.Parties).WithOne().HasForeignKey(p => p.LitigationId).OnDelete(DeleteBehavior.Restrict);
    builder.HasMany(x => x.Hearings).WithOne().HasForeignKey(h => h.LitigationId).OnDelete(DeleteBehavior.Restrict);
    builder.Navigation(x => x.Parties).UsePropertyAccessMode(PropertyAccessMode.Field);
    builder.Navigation(x => x.Hearings).UsePropertyAccessMode(PropertyAccessMode.Field);
  }
}

/// litigation_party — no audit columns in the ERD.
public class LitigationPartyConfiguration : EntityConfiguration<LitigationParty, LitigationPartyId>
{
  protected override AuditColumns Audit => AuditColumns.None;

  public override void Configure(EntityTypeBuilder<LitigationParty> builder)
  {
    base.Configure(builder);
    builder.ToTable("litigation_party");

    builder.HasKey(x => x.Id);
    builder.Property(x => x.Id).HasConversion(id => id.Value, value => LitigationPartyId.Of(value)).ValueGeneratedNever();
    builder.Property(x => x.LitigationId).HasConversion(id => id.Value, value => LitigationId.Of(value)).IsRequired();
    builder.Property(x => x.PartyName).HasMaxLength(200).IsRequired();
    builder.Property(x => x.PartyOwnerId).HasConversion(id => id!.Value, value => OwnerId.Of(value));
    // PLAINTIFF, DEFENDANT, PETITIONER, RESPONDENT, INTERVENER
    builder.Property(x => x.PartyRole).HasUpperSnakeEnum(30).IsRequired();
    builder.Property(x => x.CounselName).HasMaxLength(150);
    builder.Property(x => x.Remarks).HasMaxLength(300);

    builder.HasOne<PropertyOwner>().WithMany().HasForeignKey(x => x.PartyOwnerId).OnDelete(DeleteBehavior.Restrict);
  }
}

/// litigation_hearing — the ERD gives it created_at only.
public class LitigationHearingConfiguration : EntityConfiguration<LitigationHearing, LitigationHearingId>
{
  protected override AuditColumns Audit => AuditColumns.CreatedAt;

  public override void Configure(EntityTypeBuilder<LitigationHearing> builder)
  {
    base.Configure(builder);
    builder.ToTable("litigation_hearing");

    builder.HasKey(x => x.Id);
    builder.Property(x => x.Id).HasConversion(id => id.Value, value => LitigationHearingId.Of(value)).ValueGeneratedNever();
    builder.Property(x => x.LitigationId).HasConversion(id => id.Value, value => LitigationId.Of(value)).IsRequired();
    builder.Property(x => x.HearingDate).IsRequired();
    builder.Property(x => x.AttendedBy).HasMaxLength(150);
  }
}

/// property_appeal — departmental appeal u/s 32.
public class PropertyAppealConfiguration : EntityConfiguration<PropertyAppeal, AppealId>
{
  public override void Configure(EntityTypeBuilder<PropertyAppeal> builder)
  {
    base.Configure(builder);
    builder.ToTable("property_appeal");

    builder.HasKey(x => x.Id);
    builder.Property(x => x.Id).HasConversion(id => id.Value, value => AppealId.Of(value));
    builder.Property(x => x.PropertyId).HasConversion(id => id.Value, value => PropertyId.Of(value)).IsRequired();
    builder.Property(x => x.AppealNo).HasBusinessCode(50).IsRequired();
    builder.Property(x => x.AppellantOwnerId).HasConversion(id => id.Value, value => OwnerId.Of(value)).IsRequired();
    builder.Property(x => x.AppealedOrderRef).HasMaxLength(100).IsRequired();
    builder.Property(x => x.AppealedOrderDate).IsRequired();
    // property_allotment, agreement_violation, property_encroachment ...
    builder.Property(x => x.OrderSourceTable).HasLowerSnakeEnum(40);
    builder.Property(x => x.AppealDate).IsRequired();
    builder.Property(x => x.AppellateAuthority).HasMaxLength(150).IsRequired().HasDefaultValue(PropertyAppeal.DefaultAppellateAuthority);
    builder.Property(x => x.DelegatedOfficer).HasMaxLength(150);
    // ALLOWED, DISMISSED, REMANDED, MODIFIED
    builder.Property(x => x.DecisionOutcome).HasNullableUpperSnakeEnum(30);
    // FILED (default), UNDER_HEARING, DECIDED, WITHDRAWN
    builder.Property(x => x.AppealStatus).HasUpperSnakeEnum(20).IsRequired().HasDefaultValue(AppealStatus.Filed);

    builder.HasIndex(x => x.AppealNo).IsUnique();

    builder.HasOne<Property>().WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<PropertyOwner>().WithMany().HasForeignKey(x => x.AppellantOwnerId).OnDelete(DeleteBehavior.Restrict);
  }
}

/// building_plan
public class BuildingPlanConfiguration : EntityConfiguration<BuildingPlan, BuildingPlanId>
{
  public override void Configure(EntityTypeBuilder<BuildingPlan> builder)
  {
    base.Configure(builder);
    builder.ToTable("building_plan");

    builder.HasKey(x => x.Id);
    builder.Property(x => x.Id).HasConversion(id => id.Value, value => BuildingPlanId.Of(value));
    builder.Property(x => x.PropertyId).HasConversion(id => id.Value, value => PropertyId.Of(value)).IsRequired();
    builder.Property(x => x.PlanNo).HasBusinessCode(50).IsRequired();
    builder.Property(x => x.BuildingPlanTypeId).HasMasterId().IsRequired();
    builder.Property(x => x.BuildingPlanStatusId).HasMasterId().IsRequired();
    builder.Property(x => x.RevisionNo).IsRequired().HasDefaultValue(0);
    builder.Property(x => x.SupersedesPlanId).HasConversion(id => id!.Value, value => BuildingPlanId.Of(value));
    builder.Property(x => x.ApplicantOwnerId).HasConversion(id => id!.Value, value => OwnerId.Of(value));
    builder.Property(x => x.ApprovedBy).HasMaxLength(150);
    builder.Property(x => x.ApprovalReferenceNo).HasMaxLength(100);
    builder.Property(x => x.CoveredArea).HasPrecision(18, 4);
    builder.Property(x => x.MeasurementUnitId).HasOptionalMasterId();
    builder.Property(x => x.ArchitectName).HasMaxLength(150);

    builder.HasIndex(x => new { x.PropertyId, x.BuildingPlanStatusId });
    builder.HasIndex(x => new { x.PlanNo, x.RevisionNo }).IsUnique();

    builder.HasOne<Property>().WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<BuildingPlanType>().WithMany().HasForeignKey(x => x.BuildingPlanTypeId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<BuildingPlanStatus>().WithMany().HasForeignKey(x => x.BuildingPlanStatusId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<BuildingPlan>().WithMany().HasForeignKey(x => x.SupersedesPlanId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<PropertyOwner>().WithMany().HasForeignKey(x => x.ApplicantOwnerId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<MeasurementUnit>().WithMany().HasForeignKey(x => x.MeasurementUnitId).OnDelete(DeleteBehavior.Restrict);
  }
}
