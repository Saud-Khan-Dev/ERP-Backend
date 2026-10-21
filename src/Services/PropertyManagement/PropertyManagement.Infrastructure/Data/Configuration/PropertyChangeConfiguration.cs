using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// property_change: the change log written by PropertyChangeLogInterceptor. Not in the ERD (the audit of 2026-10-20 asked
/// for "who changed what"); no foreign keys, so a log line outlives anything.
public class PropertyChangeConfiguration : IEntityTypeConfiguration<PropertyChange>
{
  public void Configure(EntityTypeBuilder<PropertyChange> builder)
  {
    builder.ToTable("property_change");
    builder.HasKey(x => x.Id);
    builder.Property(x => x.Entity).HasMaxLength(60).IsRequired();
    builder.Property(x => x.EntityCode).HasMaxLength(100);
    builder.Property(x => x.Action).HasMaxLength(20).IsRequired();
    builder.Property(x => x.Field).HasMaxLength(100);
    builder.Property(x => x.OldValue).HasMaxLength(PropertyChange.TextMaxLength);
    builder.Property(x => x.NewValue).HasMaxLength(PropertyChange.TextMaxLength);
    builder.Property(x => x.ChangedByName).HasMaxLength(150);
    builder.Property(x => x.ChangedAt).IsRequired();

    builder.HasIndex(x => new { x.PropertyId, x.ChangedAt });
    builder.HasIndex(x => new { x.OwnerId, x.ChangedAt });
  }
}
