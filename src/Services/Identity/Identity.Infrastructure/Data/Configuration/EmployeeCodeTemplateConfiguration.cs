using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class EmployeeCodeTemplateConfiguration : EntityConfiguration<EmployeeCodeTemplate, EmployeeCodeTemplateId>
{
  public override void Configure(EntityTypeBuilder<EmployeeCodeTemplate> builder)
  {
    base.Configure(builder);

    builder.ToTable("employee_code_templates", t =>
    {
      t.HasCheckConstraint("ck_employee_code_templates_minimum_digits",
        $"minimum_digits BETWEEN 1 AND {EmployeeCodeTemplate.MaxMinimumDigits}");
      t.HasCheckConstraint("ck_employee_code_templates_next_number", "next_number >= 1");
    });

    builder.HasKey(x => x.Id);
    builder.Property(x => x.Id).HasConversion(id => id.Value, dbId => EmployeeCodeTemplateId.Of(dbId));

    builder.Property(x => x.Prefix).HasMaxLength(EmployeeCodeTemplate.MaxPrefixLength).IsRequired();
    // empty string is a valid separator (EMP101), so required rather than nullable
    builder.Property(x => x.Separator).HasMaxLength(1).IsRequired();
    builder.Property(x => x.MinimumDigits).IsRequired();
    builder.Property(x => x.NextNumber).IsRequired();

    builder.Ignore(x => x.Pattern);
    builder.Ignore(x => x.NextCode);

    // Every issued code bumps next_number; the xmin row version turns two simultaneous issues into
    // a 409 for the loser instead of two accounts with the same code.
    builder.Property<uint>("xmin").HasColumnName("xmin").IsRowVersion();
  }
}
