using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class CurrencyLookupConfiguration : EntityConfiguration<CurrencyLookup, Currency>
{
  public override void Configure(EntityTypeBuilder<CurrencyLookup> builder)
  {
    base.Configure(builder);

    builder.ToTable("currency");

    builder.HasKey(x => x.Id);
    builder.Property(x => x.Id)
      .HasColumnName("code")
      .HasConversion(currency => currency.Value, dbValue => Currency.Of(dbValue))
      .HasColumnType("char(3)")
      .IsRequired();

    builder.Property(x => x.Name)
      .HasConversion(name => name.Value, dbValue => Name.Of(dbValue, 100))
      .HasMaxLength(100)
      .IsRequired();

    builder.Property(x => x.Symbol).HasMaxLength(10).IsRequired(false);
    builder.Property(x => x.MinorUnits).IsRequired().HasDefaultValue((short)2);
    builder.Property(x => x.IsActive).IsRequired().HasDefaultValue(true);
  }
}
