using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class OptionSetValueConfiguration : EntityConfiguration<OptionSetValue, OptionSetValueId>
{
  public override void Configure(EntityTypeBuilder<OptionSetValue> builder)
  {
    base.Configure(builder);

    builder.ToTable("option_set_value");

    builder.HasKey(x => x.Id);
    builder.Property(x => x.Id).HasConversion(id => id.Value, dbId => OptionSetValueId.Of(dbId));

    builder.Property(x => x.OptionSetId)
      .HasConversion(id => id.Value, dbId => OptionSetId.Of(dbId))
      .IsRequired();

    builder.Property(x => x.ParentValueId)
      .HasConversion(id => id!.Value, dbId => OptionSetValueId.Of(dbId))
      .IsRequired(false);

    builder.Property(x => x.Value)
      .HasConversion(code => code.Value, dbValue => LookupCode.Of(dbValue))
      .HasMaxLength(255)
      .IsRequired();

    builder.Property(x => x.Label).HasMaxLength(255).IsRequired();
    builder.Property(x => x.Color).HasMaxLength(20).IsRequired(false);
    builder.Property(x => x.Icon).HasMaxLength(100).IsRequired(false);
    builder.Property(x => x.DisplayOrder).IsRequired(false);
    builder.Property(x => x.IsActive).IsRequired().HasDefaultValue(true);

    builder.HasIndex(x => x.OptionSetId);
    builder.HasIndex(x => x.ParentValueId);
    builder.HasIndex(x => new { x.OptionSetId, x.Value }).IsUnique();

    builder.HasOne<OptionSetValue>()
      .WithMany()
      .HasForeignKey(x => x.ParentValueId)
      .IsRequired(false)
      .OnDelete(DeleteBehavior.Restrict);
  }
}
