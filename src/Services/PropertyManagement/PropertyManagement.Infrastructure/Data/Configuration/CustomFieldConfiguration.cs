using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class AttributeDefinitionConfiguration : EntityConfiguration<AttributeDefinition, AttributeDefinitionId>
{
  public override void Configure(EntityTypeBuilder<AttributeDefinition> builder)
  {
    base.Configure(builder);
    builder.ToTable("attribute_definition");

    builder.HasKey(x => x.Id);
    builder.Property(x => x.Id).HasConversion(id => id.Value, value => AttributeDefinitionId.Of(value));
    builder.Property(x => x.AttributeGroupId).HasMasterId().IsRequired();
    builder.Property(x => x.Code).HasConversion(c => c.Value, value => MasterCode.Of(value)).HasMaxLength(MasterCode.MaxLength).IsRequired();
    builder.Property(x => x.Label).HasConversion(l => l.Value, value => Name.Of(value, 100)).HasMaxLength(100).IsRequired();
    builder.Property(x => x.DataType).HasUpperSnakeEnum().IsRequired();
    builder.Property(x => x.OptionsCsv).HasMaxLength(2000);
    builder.Property(x => x.DefaultValue).HasMaxLength(AttributeDefinition.MaxTextLength);
    builder.Property(x => x.IsActive).IsRequired().HasDefaultValue(true);
    builder.Ignore(x => x.Options);

    builder.HasIndex(x => x.Code).IsUnique();
    builder.HasIndex(x => x.AttributeGroupId);

    builder.HasOne<AttributeGroup>().WithMany().HasForeignKey(x => x.AttributeGroupId).OnDelete(DeleteBehavior.Restrict);
  }
}

public class PropertyAttributeValueConfiguration : EntityConfiguration<PropertyAttributeValue, PropertyAttributeValueId>
{
  public override void Configure(EntityTypeBuilder<PropertyAttributeValue> builder)
  {
    base.Configure(builder);
    builder.ToTable("property_attribute_value");

    builder.HasKey(x => x.Id);
    builder.Property(x => x.Id).HasConversion(id => id.Value, value => PropertyAttributeValueId.Of(value));
    builder.Property(x => x.PropertyId).HasConversion(id => id.Value, value => PropertyId.Of(value)).IsRequired();
    builder.Property(x => x.AttributeDefinitionId).HasConversion(id => id.Value, value => AttributeDefinitionId.Of(value)).IsRequired();
    builder.Property(x => x.DataType).HasUpperSnakeEnum().IsRequired();
    builder.Property(x => x.Value).HasMaxLength(AttributeDefinition.MaxTextLength);

    builder.HasIndex(x => new { x.PropertyId, x.AttributeDefinitionId }).IsUnique();

    builder.HasOne<Property>().WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<AttributeDefinition>().WithMany().HasForeignKey(x => x.AttributeDefinitionId).OnDelete(DeleteBehavior.Restrict);
  }
}

public class CodeSequenceConfiguration : EntityConfiguration<CodeSequence, CodeSequenceId>
{
  public override void Configure(EntityTypeBuilder<CodeSequence> builder)
  {
    base.Configure(builder);
    builder.ToTable("code_sequence", t =>
    {
      t.HasCheckConstraint("ck_code_sequence_minimum_digits", $"minimum_digits BETWEEN 1 AND {CodeSequence.MaxMinimumDigits}");
      t.HasCheckConstraint("ck_code_sequence_next_number", "next_number >= 1");
    });

    builder.HasKey(x => x.Id);
    builder.Property(x => x.Id).HasConversion(id => id.Value, value => CodeSequenceId.Of(value));
    builder.Property(x => x.Key).HasConversion(k => k.Value, value => MasterCode.Of(value)).HasMaxLength(MasterCode.MaxLength).IsRequired();
    builder.Property(x => x.Prefix).HasMaxLength(CodeSequence.MaxPrefixLength).IsRequired();
    // empty is a valid separator (PROP00001), so required rather than nullable
    builder.Property(x => x.Separator).HasMaxLength(1).IsRequired();
    builder.Ignore(x => x.Pattern);
    builder.Ignore(x => x.NextCode);

    builder.HasIndex(x => x.Key).IsUnique();

    // every issued code bumps next_number: the row version turns two simultaneous issues into a 409
    builder.Property<uint>("xmin").HasColumnName("xmin").IsRowVersion();
  }
}
