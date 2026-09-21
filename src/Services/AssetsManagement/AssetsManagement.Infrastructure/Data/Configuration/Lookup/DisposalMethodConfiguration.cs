using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class DisposalMethodConfiguration : EntityConfiguration<DisposalMethod, DisposalMethodId>
{
  public override void Configure(EntityTypeBuilder<DisposalMethod> builder)
  {
    base.Configure(builder);

    builder.ToTable("disposal_method");

    builder.HasKey(x => x.Id);
    builder.Property(x => x.Id).HasConversion(id => id.Value, dbId => DisposalMethodId.Of(dbId));

    builder.Property(x => x.Code)
      .HasConversion(code => code.Value, dbValue => LookupCode.Of(dbValue))
      .HasMaxLength(50)
      .IsRequired();

    builder.Property(x => x.Name)
      .HasConversion(name => name.Value, dbValue => Name.Of(dbValue, 100))
      .HasMaxLength(100)
      .IsRequired();

    builder.Property(x => x.RequiresValue).IsRequired().HasDefaultValue(false);
    builder.Property(x => x.IsActive).IsRequired().HasDefaultValue(true);

    builder.HasIndex(x => x.Code).IsUnique();
  }
}
