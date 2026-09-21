using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class AttributeDefinitionConfiguration : EntityConfiguration<AttributeDefinition, AttributeDefinitionId>
{
  public override void Configure(EntityTypeBuilder<AttributeDefinition> builder)
  {
    base.Configure(builder);

    builder.ToTable("attribute_definition");

    builder.HasKey(x => x.Id);
    builder.Property(x => x.Id).HasConversion(id => id.Value, dbId => AttributeDefinitionId.Of(dbId));

    builder.Property(x => x.Code)
      .HasConversion(code => code.Value, dbValue => AttributeCode.Of(dbValue))
      .HasMaxLength(100)
      .IsRequired();

    builder.Property(x => x.Name)
      .HasConversion(name => name.Value, dbValue => Name.Of(dbValue, 150))
      .HasMaxLength(150)
      .IsRequired();

    builder.Property(x => x.Description).IsRequired(false);
    builder.Property(x => x.DataType).HasEnumString().IsRequired();

    builder.Property(x => x.OptionSetId)
      .HasConversion(id => id!.Value, dbId => OptionSetId.Of(dbId))
      .IsRequired(false);

    builder.Property(x => x.ReferenceEntity).HasMaxLength(100).IsRequired(false);
    builder.Property(x => x.Unit).HasMaxLength(50).IsRequired(false);
    builder.Property(x => x.NumericPrecision).IsRequired(false);
    builder.Property(x => x.NumericScale).IsRequired(false);

    builder.Property(x => x.MinNumber).HasPrecision(18, 4).IsRequired(false);
    builder.Property(x => x.MaxNumber).HasPrecision(18, 4).IsRequired(false);
    builder.Property(x => x.MinLength).IsRequired(false);
    builder.Property(x => x.MaxLength).IsRequired(false);
    builder.Property(x => x.MinDate).IsRequired(false);
    builder.Property(x => x.MaxDate).IsRequired(false);
    builder.Property(x => x.RegexPattern).IsRequired(false);
    builder.Property(x => x.IsUniquePerCategory).IsRequired().HasDefaultValue(false);
    builder.Property(x => x.ValidationMessage).IsRequired(false);

    builder.Property(x => x.IsMultiValue).IsRequired().HasDefaultValue(false);
    builder.Property(x => x.IsPii).IsRequired().HasDefaultValue(false);
    builder.Property(x => x.IsSystem).IsRequired().HasDefaultValue(false);
    builder.Property(x => x.IsActive).IsRequired().HasDefaultValue(true);

    builder.Ignore(x => x.RequiresOptionSet);

    builder.HasIndex(x => x.Code).IsUnique();
    builder.HasIndex(x => x.DataType);
    builder.HasIndex(x => x.OptionSetId);

    builder.HasOne<OptionSet>()
      .WithMany()
      .HasForeignKey(x => x.OptionSetId)
      .IsRequired(false)
      .OnDelete(DeleteBehavior.Restrict);

    builder.ToTable(t =>
    {
      t.HasCheckConstraint("ck_attribute_definition_option_set",
        "(data_type NOT IN ('Select','MultiSelect')) OR option_set_id IS NOT NULL");
      t.HasCheckConstraint("ck_attribute_definition_reference_entity",
        "(data_type <> 'Reference') OR reference_entity IS NOT NULL");
      t.HasCheckConstraint("ck_attribute_definition_number_range",
        "min_number IS NULL OR max_number IS NULL OR min_number <= max_number");
      t.HasCheckConstraint("ck_attribute_definition_length_range",
        "min_length IS NULL OR max_length IS NULL OR min_length <= max_length");
      t.HasCheckConstraint("ck_attribute_definition_date_range",
        "min_date IS NULL OR max_date IS NULL OR min_date <= max_date");
    });
  }
}
