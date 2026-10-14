using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

// 1. organisation and 2. rate masters (docs/gda_taxation_module.dbml)

public class TaxCircleConfiguration : EntityConfiguration<TaxCircle, TaxCircleId>
{
  public override void Configure(EntityTypeBuilder<TaxCircle> builder)
  {
    base.Configure(builder);
    builder.ToTable("tax_circle");
    builder.Property(x => x.Code).HasMaxLength(20).IsRequired();
    builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
    builder.Property(x => x.OfficeAddress).HasMaxLength(300);
    builder.Property(x => x.OfficeFax).HasMaxLength(30);
    builder.Property(x => x.OfficePhone).HasMaxLength(30);
    builder.Property(x => x.SortOrder).HasDefaultValue(0);
    builder.Property(x => x.IsActive).HasDefaultValue(true);
    builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("tax_circle_code_key");
    builder.HasMany(x => x.Towns).WithOne().HasForeignKey(t => t.CircleId).OnDelete(DeleteBehavior.Cascade);
    builder.Navigation(x => x.Towns).UsePropertyAccessMode(PropertyAccessMode.Field);
  }
}

public class TaxCircleTownConfiguration : EntityConfiguration<TaxCircleTown, TaxCircleTownId>
{
  public override void Configure(EntityTypeBuilder<TaxCircleTown> builder)
  {
    base.Configure(builder);
    builder.ToTable("tax_circle_town");
    builder.Property(x => x.TownCode).HasMaxLength(20).IsRequired();
    builder.Property(x => x.ShortCode).HasMaxLength(5);
    builder.HasIndex(x => new { x.TownId, x.EffectiveFrom }).IsUnique().HasDatabaseName("tax_circle_town_town_id_effective_from_key");
  }
}

public class TaxCircleStaffConfiguration : EntityConfiguration<TaxCircleStaff, TaxCircleStaffId>
{
  public override void Configure(EntityTypeBuilder<TaxCircleStaff> builder)
  {
    base.Configure(builder);
    builder.ToTable("tax_circle_staff");
    builder.Property(x => x.TaxRole).UpperSnake(20).IsRequired();
    builder.HasOne<TaxCircle>().WithMany().HasForeignKey(x => x.CircleId).OnDelete(DeleteBehavior.Restrict);
  }
}

public class TaxHeadConfiguration : EntityConfiguration<TaxHead, TaxHeadId>
{
  public override void Configure(EntityTypeBuilder<TaxHead> builder)
  {
    base.Configure(builder);
    builder.ToTable("tax_head");
    builder.Property(x => x.Code).HasMaxLength(30).IsRequired();
    builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
    builder.Property(x => x.HeadKind).UpperSnake(10).IsRequired();
    builder.Property(x => x.IsRefundable).HasDefaultValue(false);
    builder.Property(x => x.CalcMethod).UpperSnake(10);
    builder.Property(x => x.IsBuiltin).HasDefaultValue(false);
    builder.Property(x => x.AppliesTo).UpperSnake(5).IsRequired().HasDefaultValueSql("'BOTH'");
    builder.Property(x => x.ChargedFrom).UpperSnake(15).IsRequired().HasDefaultValueSql("'ALWAYS'");
    builder.Property(x => x.FacilityLabel).HasMaxLength(60);
    builder.Property(x => x.FormulaCom).HasMaxLength(500);
    builder.Property(x => x.FormulaRes).HasMaxLength(500);
    builder.Property(x => x.BoardReference).HasMaxLength(100);
    builder.Property(x => x.AppliesAnnualIncrease).HasDefaultValue(false);
    builder.Property(x => x.IsConditional).HasDefaultValue(false);
    builder.Property(x => x.OverrideAllowed).HasDefaultValue(true);
    builder.Property(x => x.AccountsHeadCode).HasMaxLength(30);
    builder.Property(x => x.SortOrder).HasDefaultValue(0);
    builder.Property(x => x.IsActive).HasDefaultValue(true);
    builder.Ignore(x => x.IsSystem);
    builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("tax_head_code_key");
  }
}

public class CategoryMapConfiguration : EntityConfiguration<CategoryMap, CategoryMapId>
{
  public override void Configure(EntityTypeBuilder<CategoryMap> builder)
  {
    base.Configure(builder);
    builder.ToTable("category_map");
    builder.Property(x => x.PropertyTypeCode).HasMaxLength(30).IsRequired();
    builder.Property(x => x.TaxCategory).UpperSnake(10).IsRequired();
    builder.HasIndex(x => x.PropertyTypeId).IsUnique().HasDatabaseName("category_map_property_type_id_key");
  }
}

public class RateSetConfiguration : EntityConfiguration<RateSet, RateSetId>
{
  public override void Configure(EntityTypeBuilder<RateSet> builder)
  {
    base.Configure(builder);
    builder.ToTable("rate_set");
    builder.Property(x => x.Name).HasMaxLength(150).IsRequired();
    builder.Property(x => x.BoardReference).HasMaxLength(100).IsRequired();
    builder.Property(x => x.Status).UpperSnake(15).IsRequired().HasDefaultValueSql("'DRAFT'");
    builder.Ignore(x => x.BaseFiscalYear);
    builder.Ignore(x => x.TownsWithFactors);
    builder.HasMany(x => x.LtfFactors).WithOne().HasForeignKey(f => f.RateSetId).OnDelete(DeleteBehavior.Cascade);
    builder.HasMany(x => x.RateBands).WithOne().HasForeignKey(b => b.RateSetId).OnDelete(DeleteBehavior.Cascade);
    builder.Navigation(x => x.LtfFactors).UsePropertyAccessMode(PropertyAccessMode.Field);
    builder.Navigation(x => x.RateBands).UsePropertyAccessMode(PropertyAccessMode.Field);
  }
}

public class LtfFactorConfiguration : EntityConfiguration<LtfFactor, LtfFactorId>
{
  public override void Configure(EntityTypeBuilder<LtfFactor> builder)
  {
    base.Configure(builder);
    builder.ToTable("ltf_factor");
    builder.Property(x => x.TaxCategory).UpperSnake(10).IsRequired();
    builder.Property(x => x.Component).UpperSnake(10).IsRequired();
    builder.Property(x => x.BandFrom).Numeric(18, 4);
    builder.Property(x => x.BandTo).Numeric(18, 4);
    builder.Property(x => x.Factor).Numeric(18, 4);
    builder.HasIndex(x => new { x.RateSetId, x.TownId, x.TaxCategory, x.Component, x.BandFrom }).IsUnique()
      .HasDatabaseName("ltf_factor_rate_set_id_town_id_tax_category_component_band_key");
  }
}

public class RateBandConfiguration : EntityConfiguration<RateBand, RateBandId>
{
  public override void Configure(EntityTypeBuilder<RateBand> builder)
  {
    base.Configure(builder);
    builder.ToTable("rate_band");
    builder.Property(x => x.TaxCategory).UpperSnake(10);
    builder.Property(x => x.Basis).UpperSnake(20).IsRequired();
    builder.Property(x => x.UnitLabel).HasMaxLength(60);
    builder.Property(x => x.RoundUp).HasDefaultValue(false);
    builder.Property(x => x.BlockSize).Numeric(18, 4);
    builder.Property(x => x.BandFrom).Numeric(18, 4);
    builder.Property(x => x.BandTo).Numeric(18, 4);
    builder.Property(x => x.Rate).Numeric(18, 4);
    builder.Property(x => x.RateType).UpperSnake(10).IsRequired();
    builder.Property(x => x.ProRata).HasDefaultValue(false);
    builder.Property(x => x.AboveTopRule).UpperSnake(20);
    builder.HasOne<TaxHead>().WithMany().HasForeignKey(x => x.HeadId).OnDelete(DeleteBehavior.Restrict);
  }
}

public class TaxSettingConfiguration : EntityConfiguration<TaxSetting, TaxSettingId>
{
  public override void Configure(EntityTypeBuilder<TaxSetting> builder)
  {
    base.Configure(builder);
    builder.ToTable("tax_setting");
    builder.Property(x => x.Key).HasMaxLength(60).IsRequired();
    builder.Property(x => x.Value).HasMaxLength(100).IsRequired();
    builder.HasOne<RateSet>().WithMany().HasForeignKey(x => x.RateSetId).OnDelete(DeleteBehavior.Restrict);
    builder.HasIndex(x => new { x.Key, x.EffectiveFrom }).IsUnique().HasDatabaseName("tax_setting_key_effective_from_key");
  }
}

public class MessageTemplateConfiguration : EntityConfiguration<MessageTemplate, MessageTemplateId>
{
  public override void Configure(EntityTypeBuilder<MessageTemplate> builder)
  {
    base.Configure(builder);
    builder.ToTable("message_template");
    builder.Property(x => x.Code).HasMaxLength(40).IsRequired();
    builder.Property(x => x.Channel).UpperSnake(10).IsRequired();
    builder.Property(x => x.Language).HasMaxLength(5).IsRequired().HasDefaultValue("en");
    builder.Property(x => x.Body).Text().IsRequired();
    builder.Property(x => x.VersionNo).HasDefaultValue(1);
    builder.Property(x => x.IsActive).HasDefaultValue(true);
  }
}

public class SpecialPolicyConfiguration : EntityConfiguration<SpecialPolicy, SpecialPolicyId>
{
  public override void Configure(EntityTypeBuilder<SpecialPolicy> builder)
  {
    base.Configure(builder);
    builder.ToTable("special_policy");
    builder.Property(x => x.Code).HasMaxLength(30);
    builder.Property(x => x.Name).HasMaxLength(150).IsRequired();
    builder.Property(x => x.Method).UpperSnake(20).IsRequired();
    builder.Property(x => x.Value).Numeric(18, 4);
    builder.Property(x => x.AppliesTo).UpperSnake(5).IsRequired().HasDefaultValueSql("'BOTH'");
    builder.Property(x => x.AnnualIncrease).HasDefaultValue(false);
    builder.Property(x => x.BoardReference).HasMaxLength(100);
    builder.Property(x => x.IsActive).HasDefaultValue(false);
    builder.HasOne<TaxHead>().WithMany().HasForeignKey(x => x.HeadId).OnDelete(DeleteBehavior.Restrict);
    builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("special_policy_code_key");
  }
}

public class PenaltyRuleConfiguration : EntityConfiguration<PenaltyRule, PenaltyRuleId>
{
  public override void Configure(EntityTypeBuilder<PenaltyRule> builder)
  {
    base.Configure(builder);
    builder.ToTable("penalty_rule");
    builder.Property(x => x.Code).HasMaxLength(30).IsRequired();
    builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
    builder.Property(x => x.Kind).UpperSnake(20).IsRequired();
    builder.Property(x => x.Trigger).UpperSnake(10).IsRequired();
    builder.Property(x => x.PeriodUnit).UpperSnake(10);
    builder.Property(x => x.PeriodAnchor).UpperSnake(15);
    builder.Property(x => x.RateType).UpperSnake(10).IsRequired();
    builder.Property(x => x.Rate).Numeric(18, 4);
    builder.Property(x => x.Base).UpperSnake(15).IsRequired().HasDefaultValueSql("'OUTSTANDING'");
    builder.Property(x => x.TargetSet).UpperSnake(15).IsRequired().HasDefaultValueSql("'ALL_UNPAID'");
    builder.Property(x => x.IncludesCurrent).HasDefaultValue(true);
    builder.HasOne<RateSet>().WithMany().HasForeignKey(x => x.RateSetId).OnDelete(DeleteBehavior.Restrict);
    builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("penalty_rule_code_key");
  }
}

public class NoticeTemplateConfiguration : EntityConfiguration<NoticeTemplate, NoticeTemplateId>
{
  public override void Configure(EntityTypeBuilder<NoticeTemplate> builder)
  {
    base.Configure(builder);
    builder.ToTable("notice_template");
    builder.Property(x => x.Stage).UpperSnake(15).IsRequired();
    builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
    builder.Property(x => x.TitleText).HasMaxLength(200).IsRequired();
    builder.Property(x => x.ParagraphText).Text().IsRequired();
    builder.Property(x => x.SourceDocument).HasMaxLength(255);
    builder.Property(x => x.VersionNo).HasDefaultValue(1);
    builder.Property(x => x.IsActive).HasDefaultValue(true);
    builder.HasIndex(x => x.Stage).IsUnique().HasDatabaseName("notice_template_stage_key");
  }
}

public class NoticeLetterheadConfiguration : EntityConfiguration<NoticeLetterhead, NoticeLetterheadId>
{
  public override void Configure(EntityTypeBuilder<NoticeLetterhead> builder)
  {
    base.Configure(builder);
    builder.ToTable("notice_letterhead");
    builder.Property(x => x.GovernmentLine).HasMaxLength(150).IsRequired();
    builder.Property(x => x.AuthorityLine).HasMaxLength(150).IsRequired();
    builder.Property(x => x.Motto).HasMaxLength(200);
    // the lists are kept behind List<string> fields; EF reads and writes them through the properties
    builder.Property(x => x.Notes).Jsonb().IsRequired().UsePropertyAccessMode(PropertyAccessMode.Property);
    builder.Property(x => x.CcLines).Jsonb().IsRequired().UsePropertyAccessMode(PropertyAccessMode.Property);
    builder.Property(x => x.QrText).HasMaxLength(200);
    builder.Property(x => x.QrBankName).HasMaxLength(60);
    builder.Property(x => x.LogoLeftRef).HasMaxLength(255);
    builder.Property(x => x.LogoRightRef).HasMaxLength(255);
    builder.Property(x => x.Watermark).HasDefaultValue(true);
    builder.Property(x => x.BoardReference).HasMaxLength(100).IsRequired();
  }
}

public class NoticeTableLayoutConfiguration : EntityConfiguration<NoticeTableLayout, NoticeTableLayoutId>
{
  public override void Configure(EntityTypeBuilder<NoticeTableLayout> builder)
  {
    base.Configure(builder);
    builder.ToTable("notice_table_layout");
    builder.Property(x => x.OtherLabel).HasMaxLength(100).IsRequired().HasDefaultValue("Other charges / earlier dues");
    builder.Property(x => x.HideZeroRows).HasDefaultValue(false);
    builder.Property(x => x.BoardReference).HasMaxLength(100).IsRequired();
    builder.Ignore(x => x.OrderedRows);
    builder.Ignore(x => x.OrderedColumns);
    builder.Ignore(x => x.DefaultColumns);
    builder.HasMany(x => x.Rows).WithOne().HasForeignKey(r => r.LayoutId).OnDelete(DeleteBehavior.Cascade);
    builder.HasMany(x => x.Columns).WithOne().HasForeignKey(c => c.LayoutId).OnDelete(DeleteBehavior.Cascade);
    builder.Navigation(x => x.Rows).UsePropertyAccessMode(PropertyAccessMode.Field);
    builder.Navigation(x => x.Columns).UsePropertyAccessMode(PropertyAccessMode.Field);
  }
}

public class NoticeTableRowConfiguration : EntityConfiguration<NoticeTableRow, NoticeTableRowId>
{
  public override void Configure(EntityTypeBuilder<NoticeTableRow> builder)
  {
    base.Configure(builder);
    builder.ToTable("notice_table_row");
    builder.Property(x => x.Label).HasMaxLength(100).IsRequired();
    builder.HasMany(x => x.Sources).WithOne().HasForeignKey(s => s.RowId).OnDelete(DeleteBehavior.Cascade);
    builder.Navigation(x => x.Sources).UsePropertyAccessMode(PropertyAccessMode.Field);
  }
}

public class NoticeTableRowSourceConfiguration : IEntityTypeConfiguration<NoticeTableRowSource>
{
  public void Configure(EntityTypeBuilder<NoticeTableRowSource> builder)
  {
    builder.ToTable("notice_table_row_source");
    builder.HasKey(x => new { x.RowId, x.SourceCode });
    builder.Property(x => x.SourceCode).HasMaxLength(40).IsRequired();
  }
}

public class NoticeTableColumnConfiguration : EntityConfiguration<NoticeTableColumn, NoticeTableColumnId>
{
  public override void Configure(EntityTypeBuilder<NoticeTableColumn> builder)
  {
    base.Configure(builder);
    builder.ToTable("notice_table_column");
    builder.Property(x => x.ColumnKey).UpperSnake(15).IsRequired();
    builder.Property(x => x.Heading).HasMaxLength(100).IsRequired();
    builder.Property(x => x.PrintedByDefault).HasDefaultValue(true);
    builder.HasIndex(x => new { x.LayoutId, x.ColumnKey }).IsUnique().HasDatabaseName("notice_table_column_layout_id_column_key_key");
  }
}
