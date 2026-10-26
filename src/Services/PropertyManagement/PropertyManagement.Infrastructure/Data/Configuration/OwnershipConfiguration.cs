using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class PropertyOwnershipConfiguration : EntityConfiguration<PropertyOwnership, OwnershipId>
{
  public override void Configure(EntityTypeBuilder<PropertyOwnership> builder)
  {
    base.Configure(builder);
    builder.ToTable("property_ownership", t =>
    {
      t.HasCheckConstraint("ck_property_ownership_share", "ownership_share_pct > 0 AND ownership_share_pct <= 100");
      t.HasCheckConstraint("ck_property_ownership_period", "effective_to IS NULL OR effective_to >= effective_from");
    });

    builder.HasKey(x => x.Id);
    builder.Property(x => x.Id).HasConversion(id => id.Value, value => OwnershipId.Of(value));
    builder.Property(x => x.PropertyId).HasConversion(id => id.Value, value => PropertyId.Of(value)).IsRequired();
    builder.Property(x => x.OwnerId).HasConversion(id => id.Value, value => OwnerId.Of(value)).IsRequired();
    builder.Property(x => x.TenureTypeId).HasMasterId().IsRequired();
    builder.Property(x => x.OwnershipSharePct).HasPrecision(7, 4).HasDefaultValue(100m);
    builder.Property(x => x.OwnershipStatus).HasUpperSnakeEnum(20).IsRequired();
    builder.Property(x => x.AcquisitionTransferTypeId).HasOptionalMasterId();
    builder.Property(x => x.AcquiredViaTransferId).HasConversion(id => id!.Value, value => TransferId.Of(value));
    builder.Property(x => x.AcquiredViaAllotmentId).HasConversion(id => id!.Value, value => AllotmentId.Of(value));
    builder.Property(x => x.ReferenceNo).HasMaxLength(100);
    builder.Property(x => x.EndReferenceNo).HasMaxLength(PropertyOwnership.ReferenceMaxLength);
    // EXACT, MONTH, YEAR, ABOUT: how well effective_from is known (old records)
    builder.Property(x => x.EffectiveFromPrecision).HasUpperSnakeEnum(12).IsRequired().HasDefaultValueSql("'EXACT'");
    builder.Ignore(x => x.IsCurrent);
    builder.Ignore(x => x.IsTransferable);

    builder.HasIndex(x => new { x.PropertyId, x.OwnershipStatus });
    builder.HasIndex(x => x.OwnerId);
    builder.HasIndex(x => new { x.PropertyId, x.OwnerId, x.EffectiveFrom }).IsUnique();
    // one current holding per owner per property (a change of share is a transfer, which closes the old row first)
    builder.HasIndex(x => new { x.PropertyId, x.OwnerId }, "ux_property_ownership_current_holding")
      .HasDatabaseName("ux_property_ownership_current_holding")
      .IsUnique()
      .HasFilter("ownership_status IN ('ACTIVE', 'DISPUTED')");

    builder.HasOne<Property>().WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<PropertyOwner>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<TenureType>().WithMany().HasForeignKey(x => x.TenureTypeId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<TransferType>().WithMany().HasForeignKey(x => x.AcquisitionTransferTypeId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<PropertyTransfer>().WithMany().HasForeignKey(x => x.AcquiredViaTransferId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<PropertyAllotment>().WithMany().HasForeignKey(x => x.AcquiredViaAllotmentId).OnDelete(DeleteBehavior.Restrict);
  }
}

public class PropertyTransferConfiguration : EntityConfiguration<PropertyTransfer, TransferId>
{
  public override void Configure(EntityTypeBuilder<PropertyTransfer> builder)
  {
    base.Configure(builder);
    builder.ToTable("property_transfer");

    builder.HasKey(x => x.Id);
    builder.Property(x => x.Id).HasConversion(id => id.Value, value => TransferId.Of(value));
    builder.Property(x => x.PropertyId).HasConversion(id => id.Value, value => PropertyId.Of(value)).IsRequired();
    builder.Property(x => x.TransferNo).HasBusinessCode(50).IsRequired();
    builder.Property(x => x.TransferTypeId).HasMasterId().IsRequired();
    builder.Property(x => x.TransferReferenceNo).HasMaxLength(100);
    // EXACT, MONTH, YEAR, ABOUT: how well transfer_date is known (old records)
    builder.Property(x => x.TransferDatePrecision).HasUpperSnakeEnum(12).IsRequired().HasDefaultValueSql("'EXACT'");
    builder.Property(x => x.ShareTransferredPct).HasPrecision(7, 4);
    builder.Property(x => x.ConsiderationAmount).HasPrecision(18, 2);
    builder.Property(x => x.Relationship).HasMaxLength(100);
    builder.Property(x => x.ApprovedBy).HasMaxLength(Approver.NameMaxLength);
    builder.Property(x => x.ApprovedByUserId);
    builder.Property(x => x.ApprovedByEmployeeId);
    builder.Property(x => x.ApprovalOrderRef).HasMaxLength(100);
    builder.Property(x => x.NoDuesCertificateNo).HasMaxLength(PropertyTransfer.NoDuesCertificateMaxLength);
    builder.Property(x => x.CompletedBy).HasMaxLength(150);
    builder.Property(x => x.ClosedBy).HasMaxLength(150);
    builder.Property(x => x.ClosingReason);
    // INITIATED, APPROVED (legacy: approved before approval completed it), COMPLETED, CANCELLED, REJECTED
    builder.Property(x => x.TransferStatus).HasUpperSnakeEnum(20).IsRequired();
    builder.Ignore(x => x.IsPending);

    builder.HasXminRowVersion();

    builder.HasIndex(x => x.TransferNo).IsUnique();
    builder.HasIndex(x => new { x.PropertyId, x.TransferDate });

    builder.HasOne<Property>().WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<TransferType>().WithMany().HasForeignKey(x => x.TransferTypeId).OnDelete(DeleteBehavior.Restrict);

    builder.HasMany(x => x.Parties).WithOne(p => p.Transfer).HasForeignKey(p => p.PropertyTransferId).OnDelete(DeleteBehavior.Restrict);
    builder.Navigation(x => x.Parties).UsePropertyAccessMode(PropertyAccessMode.Field);
  }
}

/// property_transfer_party — no audit columns in the ERD (the transfer header carries them).
public class PropertyTransferPartyConfiguration : EntityConfiguration<PropertyTransferParty, TransferPartyId>
{
  protected override AuditColumns Audit => AuditColumns.None;

  public override void Configure(EntityTypeBuilder<PropertyTransferParty> builder)
  {
    base.Configure(builder);
    builder.ToTable("property_transfer_party", t =>
      t.HasCheckConstraint("ck_property_transfer_party_share", "share_pct > 0 AND share_pct <= 100"));

    builder.HasKey(x => x.Id);
    builder.Property(x => x.Id).HasConversion(id => id.Value, value => TransferPartyId.Of(value)).ValueGeneratedNever();
    builder.Property(x => x.PropertyTransferId).HasConversion(id => id.Value, value => TransferId.Of(value)).IsRequired();
    builder.Property(x => x.OwnerId).HasConversion(id => id.Value, value => OwnerId.Of(value)).IsRequired();
    builder.Property(x => x.PartyRole).HasUpperSnakeEnum(15).IsRequired();
    builder.Property(x => x.SharePct).HasPrecision(7, 4);

    builder.HasIndex(x => new { x.PropertyTransferId, x.OwnerId, x.PartyRole }).IsUnique();

    builder.HasOne<PropertyOwner>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
  }
}

public class PropertyEncumbranceConfiguration : EntityConfiguration<PropertyEncumbrance, EncumbranceId>
{
  public override void Configure(EntityTypeBuilder<PropertyEncumbrance> builder)
  {
    base.Configure(builder);
    builder.ToTable("property_encumbrance");

    builder.HasKey(x => x.Id);
    builder.Property(x => x.Id).HasConversion(id => id.Value, value => EncumbranceId.Of(value));
    builder.Property(x => x.PropertyId).HasConversion(id => id.Value, value => PropertyId.Of(value)).IsRequired();
    builder.Property(x => x.EncumbranceTypeId).HasMasterId().IsRequired();
    builder.Property(x => x.OwnershipId).HasConversion(id => id!.Value, value => OwnershipId.Of(value));
    builder.Property(x => x.HolderName).HasMaxLength(200).IsRequired();
    builder.Property(x => x.HolderOwnerId).HasConversion(id => id!.Value, value => OwnerId.Of(value));
    builder.Property(x => x.ReferenceNo).HasMaxLength(100);
    builder.Property(x => x.Amount).HasPrecision(18, 2);
    builder.Property(x => x.ReleaseReferenceNo).HasMaxLength(100);
    builder.Property(x => x.Status).HasUpperSnakeEnum(20).IsRequired();

    builder.HasIndex(x => new { x.PropertyId, x.Status });

    builder.HasOne<Property>().WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<EncumbranceType>().WithMany().HasForeignKey(x => x.EncumbranceTypeId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<PropertyOwnership>().WithMany().HasForeignKey(x => x.OwnershipId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<PropertyOwner>().WithMany().HasForeignKey(x => x.HolderOwnerId).OnDelete(DeleteBehavior.Restrict);
  }
}

/// ownership_history_entry - a property's ownership from the old records, entered at once and approved once. The steps
/// are kept as one jsonb document (they are read and written only as a whole, with the entry).
public class OwnershipHistoryEntryConfiguration : EntityConfiguration<OwnershipHistoryEntry, HistoryEntryId>
{
  private static readonly System.Text.Json.JsonSerializerOptions Json = new(System.Text.Json.JsonSerializerDefaults.Web)
  {
    Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
  };

  private static string Write(IReadOnlyList<HistoryStep> steps) => System.Text.Json.JsonSerializer.Serialize(steps, Json);

  private static IReadOnlyList<HistoryStep> Read(string json) =>
      System.Text.Json.JsonSerializer.Deserialize<List<HistoryStep>>(json, Json) ?? new List<HistoryStep>();

  public override void Configure(EntityTypeBuilder<OwnershipHistoryEntry> builder)
  {
    base.Configure(builder);
    builder.ToTable("ownership_history_entry");

    builder.HasKey(x => x.Id);
    builder.Property(x => x.Id).HasConversion(id => id.Value, value => HistoryEntryId.Of(value));
    builder.Property(x => x.PropertyId).HasConversion(id => id.Value, value => PropertyId.Of(value)).IsRequired();
    builder.Property(x => x.EntryNo).HasBusinessCode(50).IsRequired();
    // WAITING, REGISTERED, REJECTED, WITHDRAWN
    builder.Property(x => x.Status).HasUpperSnakeEnum(20).IsRequired();
    builder.Property(x => x.Steps)
      .HasColumnType("jsonb")
      .UsePropertyAccessMode(PropertyAccessMode.Property)
      .HasConversion(
        steps => Write(steps),
        json => Read(json),
        new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<IReadOnlyList<HistoryStep>>(
          (a, b) => Write(a!) == Write(b!),
          steps => Write(steps).GetHashCode(),
          steps => Read(Write(steps))))
      .IsRequired();
    builder.Property(x => x.Remarks);
    builder.Property(x => x.RecordedBy).HasMaxLength(Approver.NameMaxLength).IsRequired();
    builder.Property(x => x.RecordedOn).IsRequired();
    builder.Property(x => x.DecidedBy).HasMaxLength(Approver.NameMaxLength);
    builder.Property(x => x.DecidedByUserId);
    builder.Property(x => x.DecidedOn);
    builder.Property(x => x.DecisionReason);
    builder.Ignore(x => x.IsWaiting);
    builder.Ignore(x => x.TransferCount);
    builder.Ignore(x => x.DocumentIds);

    builder.HasXminRowVersion();

    builder.HasIndex(x => x.EntryNo).IsUnique();
    builder.HasIndex(x => new { x.PropertyId, x.Status });

    builder.HasOne<Property>().WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
  }
}
