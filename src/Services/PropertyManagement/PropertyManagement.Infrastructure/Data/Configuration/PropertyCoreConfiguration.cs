using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class PropertyConfiguration : EntityConfiguration<Property, PropertyId>
{
  public override void Configure(EntityTypeBuilder<Property> builder)
  {
    base.Configure(builder);
    builder.ToTable("property");

    builder.HasKey(x => x.Id);
    builder.Property(x => x.Id).HasConversion(id => id.Value, value => PropertyId.Of(value));

    builder.Property(x => x.PropertyCode).HasBusinessCode(20).IsRequired();

    // complex property rather than a converter so name search translates to SQL LIKE
    builder.ComplexProperty(x => x.PropertyName, name =>
      name.Property(n => n.Value).HasColumnName("property_name").HasMaxLength(Property.NameMaxLength).IsRequired());

    builder.Property(x => x.TownId).HasMasterId().IsRequired();
    builder.Property(x => x.PropertyTypeId).HasMasterId().IsRequired();
    builder.Property(x => x.PropertyStatusId).HasMasterId().IsRequired();
    builder.Property(x => x.PropertyClassificationId).HasMasterId().IsRequired();

    builder.Property(x => x.AddressLine).HasMaxLength(300);
    builder.Property(x => x.KhasraSurveyNo).HasMaxLength(100);
    builder.Property(x => x.IsActive).IsRequired().HasDefaultValue(true);

    builder.HasIndex(x => x.PropertyCode).IsUnique();
    builder.HasIndex(x => x.TownId);
    builder.HasIndex(x => x.PropertyTypeId);
    builder.HasIndex(x => x.PropertyStatusId);
    builder.HasIndex(x => x.PropertyClassificationId);

    builder.HasOne<Town>().WithMany().HasForeignKey(x => x.TownId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<PropertyType>().WithMany().HasForeignKey(x => x.PropertyTypeId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<PropertyStatus>().WithMany().HasForeignKey(x => x.PropertyStatusId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<PropertyClassification>().WithMany().HasForeignKey(x => x.PropertyClassificationId).OnDelete(DeleteBehavior.Restrict);
  }
}

/// property_status_history — the ERD gives it created_at / created_by only.
public class PropertyStatusHistoryConfiguration : EntityConfiguration<PropertyStatusHistory, PropertyStatusHistoryId>
{
  protected override AuditColumns Audit => AuditColumns.Created;

  public override void Configure(EntityTypeBuilder<PropertyStatusHistory> builder)
  {
    base.Configure(builder);
    builder.ToTable("property_status_history", t =>
      t.HasCheckConstraint("ck_property_status_history_period", "effective_to IS NULL OR effective_to >= effective_from"));

    builder.HasKey(x => x.Id);
    builder.Property(x => x.Id).HasConversion(id => id.Value, value => PropertyStatusHistoryId.Of(value));
    builder.Property(x => x.PropertyId).HasConversion(id => id.Value, value => PropertyId.Of(value)).IsRequired();
    builder.Property(x => x.PropertyStatusId).HasMasterId().IsRequired();
    builder.Property(x => x.Reason).HasMaxLength(300);
    builder.Property(x => x.ReferenceNo).HasMaxLength(100);
    builder.Ignore(x => x.IsOpen);

    // "one open period per property" is enforced by Property.ChangeStatus, not a partial unique index:
    // closing the old row and opening the new one happen in one save, and EF does not guarantee the
    // update runs before the insert.
    builder.HasIndex(x => new { x.PropertyId, x.EffectiveFrom });

    builder.HasOne<Property>().WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<PropertyStatus>().WithMany().HasForeignKey(x => x.PropertyStatusId).OnDelete(DeleteBehavior.Restrict);
  }
}

public class PropertyMeasurementConfiguration : EntityConfiguration<PropertyMeasurement, PropertyMeasurementId>
{
  public override void Configure(EntityTypeBuilder<PropertyMeasurement> builder)
  {
    base.Configure(builder);
    builder.ToTable("property_measurement", t =>
    {
      t.HasCheckConstraint("ck_property_measurement_total_area", "total_area > 0");
      t.HasCheckConstraint("ck_property_measurement_built_up_area", "built_up_area >= 0");
    });

    builder.HasKey(x => x.Id);
    builder.Property(x => x.Id).HasConversion(id => id.Value, value => PropertyMeasurementId.Of(value));
    builder.Property(x => x.PropertyId).HasConversion(id => id.Value, value => PropertyId.Of(value)).IsRequired();
    builder.Property(x => x.MeasurementUnitId).HasMasterId().IsRequired();

    builder.Property(x => x.TotalArea).HasPrecision(18, 4);
    builder.Property(x => x.BuiltUpArea).HasPrecision(18, 4).HasDefaultValue(0m);
    builder.Property(x => x.TotalAreaBase).HasPrecision(18, 4);
    builder.Property(x => x.BuiltUpAreaBase).HasPrecision(18, 4);
    builder.Property(x => x.MeasurementSource).HasMaxLength(150);
    builder.Property(x => x.IsCurrent).IsRequired().HasDefaultValue(true);

    // one current row per property is kept by RecordMeasurement (same reason as status history)
    builder.HasIndex(x => new { x.PropertyId, x.IsCurrent });

    builder.HasOne<Property>().WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<MeasurementUnit>().WithMany().HasForeignKey(x => x.MeasurementUnitId).OnDelete(DeleteBehavior.Restrict);
  }
}

public class PropertyAreaRegularizationConfiguration : EntityConfiguration<PropertyAreaRegularization, AreaRegularizationId>
{
  public override void Configure(EntityTypeBuilder<PropertyAreaRegularization> builder)
  {
    base.Configure(builder);
    builder.ToTable("property_area_regularization", t =>
      t.HasCheckConstraint("ck_property_area_regularization_area", "additional_area > 0"));

    builder.HasKey(x => x.Id);
    builder.Property(x => x.Id).HasConversion(id => id.Value, value => AreaRegularizationId.Of(value));
    builder.Property(x => x.PropertyId).HasConversion(id => id.Value, value => PropertyId.Of(value)).IsRequired();
    builder.Property(x => x.MeasurementUnitId).HasMasterId().IsRequired();

    builder.Property(x => x.AdditionalArea).HasPrecision(18, 4);
    builder.Property(x => x.AdditionalAreaBase).HasPrecision(18, 4);
    builder.Property(x => x.RegularizationStatus).HasUpperSnakeEnum().IsRequired();
    builder.Property(x => x.OrderReferenceNo).HasMaxLength(100);
    builder.Property(x => x.ApprovedBy).HasMaxLength(150);
    builder.Property(x => x.IsActive).IsRequired().HasDefaultValue(true);

    builder.HasIndex(x => x.PropertyId);

    builder.HasOne<Property>().WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<MeasurementUnit>().WithMany().HasForeignKey(x => x.MeasurementUnitId).OnDelete(DeleteBehavior.Restrict);
  }
}
