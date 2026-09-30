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
    builder.Ignore(x => x.IsCurrent);

    builder.HasIndex(x => new { x.PropertyId, x.OwnershipStatus });
    builder.HasIndex(x => x.OwnerId);
    builder.HasIndex(x => new { x.PropertyId, x.OwnerId, x.EffectiveFrom }).IsUnique();

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
    builder.Property(x => x.ShareTransferredPct).HasPrecision(7, 4);
    builder.Property(x => x.ConsiderationAmount).HasPrecision(18, 2);
    builder.Property(x => x.Relationship).HasMaxLength(100);
    builder.Property(x => x.ApprovedBy).HasMaxLength(150);
    builder.Property(x => x.TransferStatus).HasUpperSnakeEnum(20).IsRequired();

    builder.HasIndex(x => x.TransferNo).IsUnique();
    builder.HasIndex(x => new { x.PropertyId, x.TransferDate });

    builder.HasOne<Property>().WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<TransferType>().WithMany().HasForeignKey(x => x.TransferTypeId).OnDelete(DeleteBehavior.Restrict);

    builder.HasMany(x => x.Parties).WithOne().HasForeignKey(p => p.PropertyTransferId).OnDelete(DeleteBehavior.Restrict);
    builder.Navigation(x => x.Parties).UsePropertyAccessMode(PropertyAccessMode.Field);
  }
}

public class PropertyTransferPartyConfiguration : EntityConfiguration<PropertyTransferParty, TransferPartyId>
{
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
