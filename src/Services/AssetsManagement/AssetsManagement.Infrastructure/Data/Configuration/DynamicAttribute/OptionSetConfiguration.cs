using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class OptionSetConfiguration : EntityConfiguration<OptionSet, OptionSetId>
{
  public override void Configure(EntityTypeBuilder<OptionSet> builder)
  {
    base.Configure(builder);

    builder.ToTable("option_set");

    builder.HasKey(x => x.Id);
    builder.Property(x => x.Id).HasConversion(id => id.Value, dbId => OptionSetId.Of(dbId));

    builder.Property(x => x.Code)
      .HasConversion(code => code.Value, dbValue => LookupCode.Of(dbValue))
      .HasMaxLength(100)
      .IsRequired();

    builder.Property(x => x.Label)
      .HasColumnName("name")
      .HasConversion(name => name.Value, dbValue => Name.Of(dbValue, 150))
      .HasMaxLength(150)
      .IsRequired();

    builder.Property(x => x.Description).IsRequired(false);
    builder.Property(x => x.IsSystem).IsRequired().HasDefaultValue(false);
    builder.Property(x => x.IsActive).IsRequired().HasDefaultValue(true);

    builder.HasIndex(x => x.Code).IsUnique();

    builder.HasMany(x => x.Values)
      .WithOne()
      .HasForeignKey(v => v.OptionSetId)
      .IsRequired()
      .OnDelete(DeleteBehavior.Cascade);

    builder.Navigation(x => x.Values)
      .HasField("_values")
      .UsePropertyAccessMode(PropertyAccessMode.Field);
  }
}
