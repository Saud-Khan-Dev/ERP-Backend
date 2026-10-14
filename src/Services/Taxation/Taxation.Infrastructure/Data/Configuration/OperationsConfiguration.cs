using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

// 3. tax files, 4. demand / notices / penalties, 5. reference and payments, 6. fees, parties, clearances, messages

public class TaxFileConfiguration : EntityConfiguration<TaxFile, TaxFileId>
{
  public override void Configure(EntityTypeBuilder<TaxFile> builder)
  {
    base.Configure(builder);
    builder.ToTable("tax_file");
    builder.Property(x => x.FileNo).HasMaxLength(40).IsRequired();
    builder.Property(x => x.PropertyCode).HasMaxLength(40).IsRequired();
    builder.Property(x => x.TaxCategory).UpperSnake(10).IsRequired();
    builder.Property(x => x.HoldingKind).UpperSnake(20).IsRequired();
    builder.Property(x => x.Psid).HasMaxLength(30);
    builder.Property(x => x.UpliftRateBasis).UpperSnake(12);
    builder.Property(x => x.UpliftPct).Numeric(6, 2);
    builder.Property(x => x.OldReferenceNo).HasMaxLength(50);
    builder.Property(x => x.PlotNo).HasMaxLength(30);
    builder.Property(x => x.TaxStatus).UpperSnake(15).IsRequired().HasDefaultValueSql("'ACTIVE'");
    builder.Property(x => x.ExemptionReason).HasMaxLength(300);
    builder.Property(x => x.Remarks).Text();
    builder.Ignore(x => x.CurrentBasis);
    builder.Ignore(x => x.IsAssessable);

    builder.HasIndex(x => x.FileNo).IsUnique().HasDatabaseName("tax_file_file_no_key");
    builder.HasIndex(x => x.PropertyId).IsUnique().HasDatabaseName("tax_file_property_id_key");
    builder.HasIndex(x => x.Psid).IsUnique().HasDatabaseName("tax_file_psid_key");
    builder.HasIndex(x => new { x.CircleId, x.TownId }).HasDatabaseName("ix_tax_file_circle_id_town_id");
    builder.HasOne<TaxCircle>().WithMany().HasForeignKey(x => x.CircleId).OnDelete(DeleteBehavior.Restrict);

    builder.HasMany(x => x.Bases).WithOne().HasForeignKey(b => b.TaxFileId).OnDelete(DeleteBehavior.Cascade);
    builder.HasMany(x => x.Payers).WithOne().HasForeignKey(p => p.TaxFileId).OnDelete(DeleteBehavior.Cascade);
    builder.HasMany(x => x.HeadValues).WithOne().HasForeignKey(v => v.TaxFileId).OnDelete(DeleteBehavior.Cascade);
    builder.HasMany(x => x.Overrides).WithOne().HasForeignKey(o => o.TaxFileId).OnDelete(DeleteBehavior.Cascade);
    builder.HasMany(x => x.Holds).WithOne().HasForeignKey(h => h.TaxFileId).OnDelete(DeleteBehavior.Cascade);
    builder.HasMany(x => x.Agreements).WithOne().HasForeignKey(a => a.TaxFileId).OnDelete(DeleteBehavior.Cascade);
    foreach (var navigation in new[] { "Bases", "Payers", "HeadValues", "Overrides", "Holds", "Agreements" })
      builder.Navigation(navigation).UsePropertyAccessMode(PropertyAccessMode.Field);
  }
}

public class TaxFileBasisConfiguration : EntityConfiguration<TaxFileBasis, TaxFileBasisId>
{
  public override void Configure(EntityTypeBuilder<TaxFileBasis> builder)
  {
    base.Configure(builder);
    builder.ToTable("tax_file_basis");
    builder.Property(x => x.LandAreaSqyd).Numeric(18, 4);
    builder.Property(x => x.LandAreaEntered).Numeric(18, 4);
    builder.Property(x => x.LandAreaUnitCode).HasMaxLength(20);
    builder.Property(x => x.LandAreaKanal).Numeric(18, 4);
    builder.Property(x => x.CoveredAreaSqft).Numeric(18, 4);
    builder.Property(x => x.CoveredAreaSource).UpperSnake(20);
    builder.Property(x => x.RegularizedAreaSqyd).Numeric(18, 4);
    builder.Property(x => x.PropertyStatusCode).HasMaxLength(30);
    builder.Property(x => x.ClassificationCode).HasMaxLength(30);
    builder.Property(x => x.SourceVersion).HasMaxLength(64);
    builder.Property(x => x.IsCurrent).HasDefaultValue(true);
    builder.Ignore(x => x.TaxableLandSqyd);
    builder.Ignore(x => x.TaxableKanal);
  }
}

public class TaxFilePayerConfiguration : EntityConfiguration<TaxFilePayer, TaxFilePayerId>
{
  public override void Configure(EntityTypeBuilder<TaxFilePayer> builder)
  {
    base.Configure(builder);
    builder.ToTable("tax_file_payer");
    builder.Property(x => x.OwnerCode).HasMaxLength(20).IsRequired();
    builder.Property(x => x.PayerRole).UpperSnake(10).IsRequired();
    builder.Property(x => x.SourceType).UpperSnake(15).IsRequired();
    builder.Property(x => x.SharePct).Numeric(7, 4);
    builder.Property(x => x.IsAddressee).HasDefaultValue(false);
    builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
    builder.Property(x => x.FatherHusbandName).HasMaxLength(200);
    builder.Property(x => x.CnicNtn).HasMaxLength(20);
    builder.Property(x => x.Mobile).HasMaxLength(30);
    builder.Property(x => x.PostalAddress).HasMaxLength(400);
  }
}

public class TaxFileHeadValueConfiguration : EntityConfiguration<TaxFileHeadValue, TaxFileHeadValueId>
{
  public override void Configure(EntityTypeBuilder<TaxFileHeadValue> builder)
  {
    base.Configure(builder);
    builder.ToTable("tax_file_head_value");
    builder.Property(x => x.Quantity).Numeric(18, 4);
    builder.HasOne<TaxHead>().WithMany().HasForeignKey(x => x.HeadId).OnDelete(DeleteBehavior.Restrict);
    builder.HasIndex(x => new { x.TaxFileId, x.HeadId }).IsUnique().HasDatabaseName("tax_file_head_value_tax_file_id_head_id_key");
  }
}

public class TaxFileOverrideConfiguration : EntityConfiguration<TaxFileOverride, TaxFileOverrideId>
{
  public override void Configure(EntityTypeBuilder<TaxFileOverride> builder)
  {
    base.Configure(builder);
    builder.ToTable("tax_file_override");
    builder.Property(x => x.Mode).UpperSnake(10).IsRequired();
    builder.Property(x => x.FollowsIncrease).HasDefaultValue(true);
    builder.Property(x => x.Reason).HasMaxLength(300).IsRequired();
    builder.Property(x => x.Reference).HasMaxLength(100);
    builder.HasOne<TaxHead>().WithMany().HasForeignKey(x => x.HeadId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<SpecialPolicy>().WithMany().HasForeignKey(x => x.PolicyId).OnDelete(DeleteBehavior.Restrict);
    builder.HasIndex(x => new { x.TaxFileId, x.HeadId, x.EffectiveFrom }).IsUnique().HasDatabaseName("tax_file_override_tax_file_id_head_id_effective_from_key");
  }
}

public class LitigationHoldConfiguration : EntityConfiguration<LitigationHold, LitigationHoldId>
{
  public override void Configure(EntityTypeBuilder<LitigationHold> builder)
  {
    base.Configure(builder);
    builder.ToTable("litigation_hold");
    builder.Property(x => x.Reference).HasMaxLength(100);
    builder.Property(x => x.Reason).HasMaxLength(300).IsRequired();
    builder.Property(x => x.SuspendsDemand).HasDefaultValue(true);
    builder.Property(x => x.ExcludesCompletionClock).HasDefaultValue(true);
  }
}

public class AgreementScheduleConfiguration : EntityConfiguration<AgreementSchedule, AgreementScheduleId>
{
  public override void Configure(EntityTypeBuilder<AgreementSchedule> builder)
  {
    base.Configure(builder);
    builder.ToTable("agreement_schedule");
    builder.Property(x => x.Kind).UpperSnake(10).IsRequired();
    builder.Property(x => x.SourceNo).HasMaxLength(50).IsRequired();
    builder.Property(x => x.Frequency).UpperSnake(10).IsRequired();
    builder.Property(x => x.StepPct).Numeric(6, 2);
    builder.Property(x => x.IsDirect99Year).HasColumnName("is_direct_99_year").HasDefaultValue(false);
    builder.Property(x => x.Status).UpperSnake(15).IsRequired().HasDefaultValueSql("'ACTIVE'");
    builder.Ignore(x => x.AnnualBase);
    builder.HasMany(x => x.Revisions).WithOne().HasForeignKey(r => r.ScheduleId).OnDelete(DeleteBehavior.Cascade);
    builder.Navigation(x => x.Revisions).UsePropertyAccessMode(PropertyAccessMode.Field);
  }
}

public class AgreementStepRevisionConfiguration : EntityConfiguration<AgreementStepRevision, AgreementStepRevisionId>
{
  public override void Configure(EntityTypeBuilder<AgreementStepRevision> builder)
  {
    base.Configure(builder);
    builder.ToTable("agreement_step_revision");
    builder.Property(x => x.StepPct).Numeric(6, 2);
    builder.Property(x => x.BoardReference).HasMaxLength(100);
  }
}

public class DemandRunConfiguration : EntityConfiguration<DemandRun, DemandRunId>
{
  public override void Configure(EntityTypeBuilder<DemandRun> builder)
  {
    base.Configure(builder);
    builder.ToTable("demand_run");
    builder.Property(x => x.Status).UpperSnake(15).IsRequired();
    builder.HasOne<TaxCircle>().WithMany().HasForeignKey(x => x.CircleId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<RateSet>().WithMany().HasForeignKey(x => x.RateSetId).OnDelete(DeleteBehavior.Restrict);
  }
}

public class DemandConfiguration : EntityConfiguration<Demand, DemandId>
{
  public override void Configure(EntityTypeBuilder<Demand> builder)
  {
    base.Configure(builder);
    builder.ToTable("demand");
    builder.Property(x => x.DemandNo).HasMaxLength(40).IsRequired();
    builder.Property(x => x.PayerName).HasMaxLength(200).IsRequired();
    builder.Property(x => x.PayerCnic).HasMaxLength(20);
    builder.Property(x => x.PayerMobile).HasMaxLength(30);
    builder.Property(x => x.PayerAddress).HasMaxLength(400);
    builder.Property(x => x.ArrearsBf).HasDefaultValue(0m);
    builder.Property(x => x.PenaltyAmount).HasDefaultValue(0m);
    builder.Property(x => x.PaidAmount).HasDefaultValue(0m);
    builder.Property(x => x.Status).UpperSnake(15).IsRequired();
    builder.Property(x => x.Stage).UpperSnake(15);
    builder.Ignore(x => x.IsOpen);
    builder.Ignore(x => x.IsIssued);
    builder.Ignore(x => x.Excess);

    builder.HasIndex(x => x.DemandNo).IsUnique().HasDatabaseName("demand_demand_no_key");
    builder.HasIndex(x => new { x.TaxFileId, x.FiscalYear }).IsUnique().HasDatabaseName("demand_tax_file_id_fiscal_year_key");
    builder.HasOne<DemandRun>().WithMany().HasForeignKey(x => x.RunId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<TaxFile>().WithMany().HasForeignKey(x => x.TaxFileId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<TaxFileBasis>().WithMany().HasForeignKey(x => x.BasisId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<TaxFilePayer>().WithMany().HasForeignKey(x => x.PayerId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<Demand>().WithMany().HasForeignKey(x => x.CarriedToDemandId).OnDelete(DeleteBehavior.Restrict);

    builder.HasMany(x => x.Lines).WithOne().HasForeignKey(l => l.DemandId).OnDelete(DeleteBehavior.Cascade);
    builder.HasMany(x => x.Penalties).WithOne().HasForeignKey(p => p.DemandId).OnDelete(DeleteBehavior.Cascade);
    builder.HasMany(x => x.ArrearsRows).WithOne().HasForeignKey(a => a.AddedToDemandId).OnDelete(DeleteBehavior.Restrict);
    foreach (var navigation in new[] { "Lines", "Penalties", "ArrearsRows" })
      builder.Navigation(navigation).UsePropertyAccessMode(PropertyAccessMode.Field);
  }
}

public class DemandLineConfiguration : EntityConfiguration<DemandLine, DemandLineId>
{
  public override void Configure(EntityTypeBuilder<DemandLine> builder)
  {
    base.Configure(builder);
    builder.ToTable("demand_line");
    builder.Property(x => x.BasisQty).Numeric(18, 4);
    builder.Property(x => x.BasisUnit).HasMaxLength(10);
    builder.Property(x => x.IncreaseAmount).HasDefaultValue(0m);
    builder.Property(x => x.UpliftAmount).HasDefaultValue(0m);
    builder.Property(x => x.LineMode).UpperSnake(10).IsRequired().HasDefaultValueSql("'NORMAL'");
    builder.Property(x => x.OverrideAmount).HasDefaultValue(0m);
    builder.Property(x => x.Explanation).HasMaxLength(500);
    builder.HasOne<TaxHead>().WithMany().HasForeignKey(x => x.HeadId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<LtfFactor>().WithMany().HasForeignKey(x => x.LtfFactorId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<RateBand>().WithMany().HasForeignKey(x => x.RateBandId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<TaxFileOverride>().WithMany().HasForeignKey(x => x.OverrideId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<SpecialPolicy>().WithMany().HasForeignKey(x => x.PolicyId).OnDelete(DeleteBehavior.Restrict);
  }
}

public class ArrearsConfiguration : EntityConfiguration<Arrears, ArrearsId>
{
  public override void Configure(EntityTypeBuilder<Arrears> builder)
  {
    base.Configure(builder);
    builder.ToTable("arrears");
    builder.Property(x => x.PenaltyAmount).HasDefaultValue(0m);
    builder.Property(x => x.Source).UpperSnake(10).IsRequired();
    builder.Property(x => x.Description).HasMaxLength(300);
    builder.Property(x => x.Reason).HasMaxLength(300);
    builder.HasOne<TaxFile>().WithMany().HasForeignKey(x => x.TaxFileId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<TaxHead>().WithMany().HasForeignKey(x => x.HeadId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<Demand>().WithMany().HasForeignKey(x => x.SourceDemandId).OnDelete(DeleteBehavior.Restrict);
  }
}

public class NoticeConfiguration : EntityConfiguration<Notice, NoticeId>
{
  public override void Configure(EntityTypeBuilder<Notice> builder)
  {
    base.Configure(builder);
    builder.ToTable("notice");
    builder.Property(x => x.NoticeNo).HasMaxLength(40).IsRequired();
    builder.Property(x => x.EndstNo).HasMaxLength(40);
    builder.Property(x => x.ShowsParagraph).HasDefaultValue(true);
    builder.Property(x => x.Columns).Jsonb();
    builder.Property(x => x.ShowsTable).HasDefaultValue(true);
    builder.Property(x => x.Particulars).Jsonb().IsRequired();
    builder.Property(x => x.Stage).UpperSnake(15).IsRequired();
    builder.Property(x => x.Origin).UpperSnake(10).IsRequired().HasDefaultValueSql("'CYCLE'");
    builder.Property(x => x.PayVia).UpperSnake(10).IsRequired().HasDefaultValueSql("'BOTH'");
    builder.Property(x => x.QrPayload).HasMaxLength(512);
    builder.Property(x => x.SmsSent).HasDefaultValue(true);
    builder.Property(x => x.Variables).Jsonb().IsRequired();
    builder.Property(x => x.EditedFields).Jsonb().UsePropertyAccessMode(PropertyAccessMode.Property);
    builder.Property(x => x.Status).UpperSnake(10).IsRequired().HasDefaultValueSql("'PENDING'");
    builder.Property(x => x.Psid).HasMaxLength(30).IsRequired();
    builder.Property(x => x.PdfDocumentRef).HasMaxLength(255);
    builder.Property(x => x.SigningOfficerName).HasMaxLength(200);
    builder.Property(x => x.ServiceMode).UpperSnake(15);

    builder.HasIndex(x => x.NoticeNo).IsUnique().HasDatabaseName("notice_notice_no_key");
    builder.HasIndex(x => x.EndstNo).IsUnique().HasDatabaseName("notice_endst_no_key");
    builder.HasOne<NoticeLetterhead>().WithMany().HasForeignKey(x => x.LetterheadId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<NoticeTableLayout>().WithMany().HasForeignKey(x => x.TableLayoutId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<Demand>().WithMany().HasForeignKey(x => x.DemandId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<NoticeTemplate>().WithMany().HasForeignKey(x => x.TemplateId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<PenaltyRun>().WithMany().HasForeignKey(x => x.PenaltyRunId).OnDelete(DeleteBehavior.Restrict);
    // notice.sms_message_id is kept without a foreign key: the notice and its SMS are written in one save and
    // sms_message.notice_id already points back (two keys between the same rows would be a cycle EF cannot order)
  }
}

public class PenaltyConfiguration : EntityConfiguration<Penalty, PenaltyId>
{
  public override void Configure(EntityTypeBuilder<Penalty> builder)
  {
    base.Configure(builder);
    builder.ToTable("penalty");
    builder.Property(x => x.Kind).UpperSnake(15).IsRequired();
    builder.Property(x => x.Pct).Numeric(6, 2);
    builder.Property(x => x.RuleRef).HasMaxLength(100);
    builder.HasOne<PenaltyRule>().WithMany().HasForeignKey(x => x.RuleId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<PenaltyRun>().WithMany().HasForeignKey(x => x.RunId).OnDelete(DeleteBehavior.Restrict);
  }
}

public class PenaltyRunConfiguration : EntityConfiguration<PenaltyRun, PenaltyRunId>
{
  public override void Configure(EntityTypeBuilder<PenaltyRun> builder)
  {
    base.Configure(builder);
    builder.ToTable("penalty_run");
    builder.Property(x => x.TargetSet).UpperSnake(15).IsRequired();
    builder.Property(x => x.RateType).UpperSnake(10).IsRequired();
    builder.Property(x => x.Rate).Numeric(18, 4);
    builder.Property(x => x.IncludesCurrent).HasDefaultValue(true);
    builder.Property(x => x.Reference).HasMaxLength(100);
    builder.HasOne<PenaltyRule>().WithMany().HasForeignKey(x => x.RuleId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<TaxCircle>().WithMany().HasForeignKey(x => x.CircleId).OnDelete(DeleteBehavior.Restrict);
  }
}

public class PsidSyncConfiguration : EntityConfiguration<PsidSync, PsidSyncId>
{
  public override void Configure(EntityTypeBuilder<PsidSync> builder)
  {
    base.Configure(builder);
    builder.ToTable("psid_sync");
    builder.Property(x => x.Psid).HasMaxLength(30).IsRequired();
    builder.Property(x => x.Reason).UpperSnake(20).IsRequired();
    builder.Property(x => x.OnelinkAck).HasMaxLength(60);
    builder.Property(x => x.Note).HasMaxLength(300);
    builder.Property(x => x.Status).UpperSnake(10).IsRequired();
    builder.HasOne<TaxFile>().WithMany().HasForeignKey(x => x.TaxFileId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<Demand>().WithMany().HasForeignKey(x => x.DemandId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<Penalty>().WithMany().HasForeignKey(x => x.PenaltyId).OnDelete(DeleteBehavior.Restrict);
  }
}

public class PaymentConfiguration : EntityConfiguration<Payment, PaymentId>
{
  public override void Configure(EntityTypeBuilder<Payment> builder)
  {
    base.Configure(builder);
    builder.ToTable("payment");
    builder.Property(x => x.Psid).HasMaxLength(30).IsRequired();
    builder.Property(x => x.Channel).UpperSnake(15).IsRequired();
    builder.Property(x => x.ReceiptNo).HasMaxLength(40);
    builder.Property(x => x.BankCode).HasMaxLength(20);
    builder.Property(x => x.TransactionRef).HasMaxLength(60).IsRequired();
    builder.Property(x => x.Status).UpperSnake(10).IsRequired();
    builder.Ignore(x => x.Allocated);
    builder.HasIndex(x => x.TransactionRef).IsUnique().HasDatabaseName("payment_transaction_ref_key");
    builder.HasOne<TaxFile>().WithMany().HasForeignKey(x => x.TaxFileId).OnDelete(DeleteBehavior.Restrict);
    builder.HasMany(x => x.Allocations).WithOne().HasForeignKey(a => a.PaymentId).OnDelete(DeleteBehavior.Cascade);
    builder.Navigation(x => x.Allocations).UsePropertyAccessMode(PropertyAccessMode.Field);
  }
}

public class PaymentAllocationConfiguration : EntityConfiguration<PaymentAllocation, PaymentAllocationId>
{
  public override void Configure(EntityTypeBuilder<PaymentAllocation> builder)
  {
    base.Configure(builder);
    builder.ToTable("payment_allocation");
    builder.Ignore(x => x.ItemId);
    builder.HasOne<TaxHead>().WithMany().HasForeignKey(x => x.HeadId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<DemandLine>().WithMany().HasForeignKey(x => x.DemandLineId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<Arrears>().WithMany().HasForeignKey(x => x.ArrearsId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<Penalty>().WithMany().HasForeignKey(x => x.PenaltyId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<Fee>().WithMany().HasForeignKey(x => x.FeeId).OnDelete(DeleteBehavior.Restrict);
  }
}

public class PartyConfiguration : EntityConfiguration<Party, PartyId>
{
  public override void Configure(EntityTypeBuilder<Party> builder)
  {
    base.Configure(builder);
    builder.ToTable("party");
    builder.Property(x => x.PartyType).UpperSnake(15).IsRequired();
    builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
    builder.Property(x => x.CnicNtn).HasMaxLength(20);
    builder.Property(x => x.Mobile).HasMaxLength(30);
    builder.Property(x => x.Address).HasMaxLength(400);
  }
}

public class FeeConfiguration : EntityConfiguration<Fee, FeeId>
{
  public override void Configure(EntityTypeBuilder<Fee> builder)
  {
    base.Configure(builder);
    builder.ToTable("fee");
    builder.Property(x => x.FeeNo).HasMaxLength(40).IsRequired();
    builder.Property(x => x.SourceType).UpperSnake(20);
    builder.Property(x => x.SourceNo).HasMaxLength(50);
    builder.Property(x => x.BasisQty).Numeric(18, 4);
    builder.Property(x => x.BasisUnit).HasMaxLength(10);
    builder.Property(x => x.ExemptionReason).HasMaxLength(200);
    builder.Property(x => x.Status).UpperSnake(10).IsRequired();
    builder.HasIndex(x => x.FeeNo).IsUnique().HasDatabaseName("fee_fee_no_key");
    builder.HasOne<TaxHead>().WithMany().HasForeignKey(x => x.HeadId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<TaxFile>().WithMany().HasForeignKey(x => x.TaxFileId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<Party>().WithMany().HasForeignKey(x => x.PartyId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<RateBand>().WithMany().HasForeignKey(x => x.RateBandId).OnDelete(DeleteBehavior.Restrict);
  }
}

public class ClearanceConfiguration : EntityConfiguration<Clearance, ClearanceId>
{
  public override void Configure(EntityTypeBuilder<Clearance> builder)
  {
    base.Configure(builder);
    builder.ToTable("clearance");
    builder.Property(x => x.ClearanceNo).HasMaxLength(40).IsRequired();
    builder.Property(x => x.Purpose).UpperSnake(15).IsRequired();
    builder.Property(x => x.PropertyRefNo).HasMaxLength(50);
    builder.Property(x => x.Status).UpperSnake(10).IsRequired();
    builder.HasIndex(x => x.ClearanceNo).IsUnique().HasDatabaseName("clearance_clearance_no_key");
    builder.HasOne<TaxFile>().WithMany().HasForeignKey(x => x.TaxFileId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<Fee>().WithMany().HasForeignKey(x => x.FeeId).OnDelete(DeleteBehavior.Restrict);
  }
}

public class SmsMessageConfiguration : EntityConfiguration<SmsMessage, SmsMessageId>
{
  public override void Configure(EntityTypeBuilder<SmsMessage> builder)
  {
    base.Configure(builder);
    builder.ToTable("sms_message");
    builder.Property(x => x.ToMobile).HasMaxLength(30).IsRequired();
    builder.Property(x => x.Body).Text().IsRequired();
    builder.Property(x => x.ProviderRef).HasMaxLength(60);
    builder.Property(x => x.DeliveryStatus).UpperSnake(15);
    builder.HasOne<TaxFile>().WithMany().HasForeignKey(x => x.TaxFileId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<Demand>().WithMany().HasForeignKey(x => x.DemandId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<Notice>().WithMany().HasForeignKey(x => x.NoticeId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<MessageTemplate>().WithMany().HasForeignKey(x => x.TemplateId).OnDelete(DeleteBehavior.Restrict);
  }
}
