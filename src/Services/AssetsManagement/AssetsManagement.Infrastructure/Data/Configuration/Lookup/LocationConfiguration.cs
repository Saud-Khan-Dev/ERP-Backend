using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class LocationConfiguration : EntityConfiguration<Location, LocationId>
{
  public override void Configure(EntityTypeBuilder<Location> builder)
  {
    base.Configure(builder);

    builder.ToTable("location");

    builder.HasKey(x => x.Id);
    builder.Property(x => x.Id).HasConversion(id => id.Value, dbId => LocationId.Of(dbId));

    builder.Property(x => x.ParentLocationId)
      .HasConversion(id => id!.Value, dbId => LocationId.Of(dbId))
      .IsRequired(false);

    builder.Property(x => x.Code)
      .HasConversion(code => code.Value, dbValue => LookupCode.Of(dbValue))
      .HasMaxLength(50)
      .IsRequired();

    builder.Property(x => x.Name)
      .HasConversion(name => name.Value, dbValue => Name.Of(dbValue, 150))
      .HasMaxLength(150)
      .IsRequired();

    builder.Property(x => x.LocationType).HasMaxLength(50).IsRequired(false);
    builder.Property(x => x.Path).HasNullableLtree().IsRequired(false);
    builder.Property(x => x.Address).IsRequired(false);
    builder.Property(x => x.Latitude).HasPrecision(9, 6).IsRequired(false);
    builder.Property(x => x.Longitude).HasPrecision(9, 6).IsRequired(false);
    builder.Property(x => x.IsActive).IsRequired().HasDefaultValue(true);

    builder.HasIndex(x => x.Code).IsUnique();
    builder.HasIndex(x => x.ParentLocationId);
    builder.HasIndex(x => x.Path).HasMethod("gist");

    builder.HasOne<Location>()
      .WithMany()
      .HasForeignKey(x => x.ParentLocationId)
      .IsRequired(false)
      .OnDelete(DeleteBehavior.Restrict);
  }
}
