using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

// =====================================================================
// 8–12. ALLOTMENT, LEASE & RENTAL, AGREEMENT VIOLATIONS, AUCTION, OUTSOURCING
// Columns, sizes and indexes follow docs/gda_property_module.dbml.
// =====================================================================

/// property_allotment
public class PropertyAllotmentConfiguration : EntityConfiguration<PropertyAllotment, AllotmentId>
{
  public override void Configure(EntityTypeBuilder<PropertyAllotment> builder)
  {
    base.Configure(builder);
    builder.ToTable("property_allotment");

    builder.HasKey(x => x.Id);
    builder.Property(x => x.Id).HasConversion(id => id.Value, value => AllotmentId.Of(value));
    builder.Property(x => x.PropertyId).HasConversion(id => id.Value, value => PropertyId.Of(value)).IsRequired();
    builder.Property(x => x.AllotteeOwnerId).HasConversion(id => id.Value, value => OwnerId.Of(value)).IsRequired();
    builder.Property(x => x.AllotmentNo).HasBusinessCode(50).IsRequired();
    builder.Property(x => x.AllotmentTypeId).HasMasterId().IsRequired();
    builder.Property(x => x.AllotmentStatusId).HasMasterId().IsRequired();
    builder.Property(x => x.AllotmentDate).IsRequired();
    builder.Property(x => x.AllotmentLetterRef).HasMaxLength(100);
    builder.Property(x => x.Conditions);
    builder.Property(x => x.CancellationReason);
    builder.Property(x => x.CancellationOrderRef).HasMaxLength(100);
    builder.Property(x => x.RestorationOrderRef).HasMaxLength(100);
    builder.Property(x => x.IsActive).IsRequired().HasDefaultValue(true);

    builder.HasIndex(x => x.AllotmentNo).IsUnique();
    builder.HasIndex(x => new { x.PropertyId, x.AllotmentStatusId });
    builder.HasIndex(x => x.AllotteeOwnerId);

    builder.HasOne<Property>().WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<PropertyOwner>().WithMany().HasForeignKey(x => x.AllotteeOwnerId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<AllotmentType>().WithMany().HasForeignKey(x => x.AllotmentTypeId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<AllotmentStatus>().WithMany().HasForeignKey(x => x.AllotmentStatusId).OnDelete(DeleteBehavior.Restrict);
  }
}

/// property_lease
public class PropertyLeaseConfiguration : EntityConfiguration<PropertyLease, LeaseId>
{
  public override void Configure(EntityTypeBuilder<PropertyLease> builder)
  {
    base.Configure(builder);
    builder.ToTable("property_lease");

    builder.HasKey(x => x.Id);
    builder.Property(x => x.Id).HasConversion(id => id.Value, value => LeaseId.Of(value));
    builder.Property(x => x.PropertyId).HasConversion(id => id.Value, value => PropertyId.Of(value)).IsRequired();
    builder.Property(x => x.LesseeOwnerId).HasConversion(id => id.Value, value => OwnerId.Of(value)).IsRequired();
    builder.Property(x => x.LeaseNo).HasBusinessCode(50).IsRequired();
    builder.Property(x => x.LeaseTypeId).HasMasterId().IsRequired();
    builder.Property(x => x.LeaseStatusId).HasMasterId().IsRequired();
    builder.Property(x => x.LeaseStartDate).IsRequired();
    builder.Property(x => x.LeaseEndDate).IsRequired();
    builder.Property(x => x.LeasePurpose).HasMaxLength(200);
    builder.Property(x => x.LeaseAmount).HasPrecision(18, 2);
    // ANNUAL, MONTHLY, ONE_TIME
    builder.Property(x => x.AmountFrequency).HasNullableUpperSnakeEnum(20);
    builder.Property(x => x.SecurityDeposit).HasPrecision(18, 2);
    builder.Property(x => x.AgreementReference).HasMaxLength(100);
    builder.Property(x => x.IsRenewable).IsRequired().HasDefaultValue(false);
    builder.Property(x => x.RenewedFromLeaseId).HasConversion(id => id!.Value, value => LeaseId.Of(value));
    builder.Property(x => x.TerminationReason);

    builder.HasIndex(x => x.LeaseNo).IsUnique();
    builder.HasIndex(x => new { x.PropertyId, x.LeaseStatusId });
    builder.HasIndex(x => x.LesseeOwnerId);
    builder.HasIndex(x => x.LeaseEndDate);

    builder.HasOne<Property>().WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<PropertyOwner>().WithMany().HasForeignKey(x => x.LesseeOwnerId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<LeaseType>().WithMany().HasForeignKey(x => x.LeaseTypeId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<LeaseStatus>().WithMany().HasForeignKey(x => x.LeaseStatusId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<PropertyLease>().WithMany().HasForeignKey(x => x.RenewedFromLeaseId).OnDelete(DeleteBehavior.Restrict);
  }
}

/// property_rental
public class PropertyRentalConfiguration : EntityConfiguration<PropertyRental, RentalId>
{
  public override void Configure(EntityTypeBuilder<PropertyRental> builder)
  {
    base.Configure(builder);
    builder.ToTable("property_rental");

    builder.HasKey(x => x.Id);
    builder.Property(x => x.Id).HasConversion(id => id.Value, value => RentalId.Of(value));
    builder.Property(x => x.PropertyId).HasConversion(id => id.Value, value => PropertyId.Of(value)).IsRequired();
    builder.Property(x => x.TenantOwnerId).HasConversion(id => id.Value, value => OwnerId.Of(value)).IsRequired();
    builder.Property(x => x.RentalNo).HasBusinessCode(50).IsRequired();
    builder.Property(x => x.RentalTypeId).HasMasterId().IsRequired();
    builder.Property(x => x.RentalStatusId).HasMasterId().IsRequired();
    builder.Property(x => x.RentalStartDate).IsRequired();
    builder.Property(x => x.RentAmount).HasPrecision(18, 2).IsRequired();
    // MONTHLY (default), QUARTERLY, ANNUAL
    builder.Property(x => x.RentFrequency).HasUpperSnakeEnum(20).IsRequired().HasDefaultValue(RentFrequency.Monthly);
    builder.Property(x => x.SecurityDeposit).HasPrecision(18, 2);
    builder.Property(x => x.AnnualIncreasePct).HasPrecision(5, 2);
    builder.Property(x => x.AgreementReference).HasMaxLength(100);
    builder.Property(x => x.RenewedFromRentalId).HasConversion(id => id!.Value, value => RentalId.Of(value));
    builder.Property(x => x.TerminationReason);

    builder.HasIndex(x => x.RentalNo).IsUnique();
    builder.HasIndex(x => new { x.PropertyId, x.RentalStatusId });
    builder.HasIndex(x => x.TenantOwnerId);

    builder.HasOne<Property>().WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<PropertyOwner>().WithMany().HasForeignKey(x => x.TenantOwnerId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<RentalType>().WithMany().HasForeignKey(x => x.RentalTypeId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<RentalStatus>().WithMany().HasForeignKey(x => x.RentalStatusId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<PropertyRental>().WithMany().HasForeignKey(x => x.RenewedFromRentalId).OnDelete(DeleteBehavior.Restrict);
  }
}

/// agreement_violation (Act s.28-A)
public class AgreementViolationConfiguration : EntityConfiguration<AgreementViolation, ViolationId>
{
  public override void Configure(EntityTypeBuilder<AgreementViolation> builder)
  {
    base.Configure(builder);

    // the ERD's check constraint (rule 4): exactly one of lease_id / rental_id / transfer_id
    builder.ToTable("agreement_violation", t =>
      t.HasCheckConstraint("ck_agreement_violation_one_agreement", "num_nonnulls(lease_id, rental_id, transfer_id) = 1"));

    builder.HasKey(x => x.Id);
    builder.Property(x => x.Id).HasConversion(id => id.Value, value => ViolationId.Of(value));
    builder.Property(x => x.PropertyId).HasConversion(id => id.Value, value => PropertyId.Of(value)).IsRequired();
    builder.Property(x => x.AgreementTypeId).HasMasterId().IsRequired();
    builder.Property(x => x.LeaseId).HasConversion(id => id!.Value, value => LeaseId.Of(value));
    builder.Property(x => x.RentalId).HasConversion(id => id!.Value, value => RentalId.Of(value));
    builder.Property(x => x.TransferId).HasConversion(id => id!.Value, value => TransferId.Of(value));
    builder.Property(x => x.ViolatorOwnerId).HasConversion(id => id.Value, value => OwnerId.Of(value)).IsRequired();
    builder.Property(x => x.ViolationDate).IsRequired();
    builder.Property(x => x.ViolationDescription).IsRequired();
    builder.Property(x => x.OccurrenceNo).IsRequired().HasDefaultValue((short)1);
    builder.Property(x => x.NoticeNo).HasMaxLength(50);
    builder.Property(x => x.FineAmount).HasPrecision(18, 2);
    // the imposing officer's Identity user id (the ERD's app_user lives in the Identity service)
    builder.Property(x => x.FineImposedBy);
    // IMPOSED, PAID, WAIVED, RECOVERY_AS_ARREARS — null until a fine is imposed
    builder.Property(x => x.FineStatus).HasNullableUpperSnakeEnum(20);
    builder.Property(x => x.LedToCancellation).IsRequired().HasDefaultValue(false);
    // OPEN (default), RECTIFIED, FINED, CANCELLED, APPEALED
    builder.Property(x => x.ViolationStatus).HasUpperSnakeEnum(20).IsRequired().HasDefaultValue(ViolationStatus.Open);

    builder.HasIndex(x => new { x.PropertyId, x.ViolationDate });
    builder.HasIndex(x => x.LeaseId);
    builder.HasIndex(x => x.RentalId);

    builder.HasOne<Property>().WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<AgreementType>().WithMany().HasForeignKey(x => x.AgreementTypeId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<PropertyLease>().WithMany().HasForeignKey(x => x.LeaseId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<PropertyRental>().WithMany().HasForeignKey(x => x.RentalId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<PropertyTransfer>().WithMany().HasForeignKey(x => x.TransferId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<PropertyOwner>().WithMany().HasForeignKey(x => x.ViolatorOwnerId).OnDelete(DeleteBehavior.Restrict);
  }
}

/// property_auction
public class PropertyAuctionConfiguration : EntityConfiguration<PropertyAuction, AuctionId>
{
  public override void Configure(EntityTypeBuilder<PropertyAuction> builder)
  {
    base.Configure(builder);
    builder.ToTable("property_auction");

    builder.HasKey(x => x.Id);
    builder.Property(x => x.Id).HasConversion(id => id.Value, value => AuctionId.Of(value));
    builder.Property(x => x.PropertyId).HasConversion(id => id.Value, value => PropertyId.Of(value)).IsRequired();
    builder.Property(x => x.AuctionNo).HasBusinessCode(50).IsRequired();
    builder.Property(x => x.AuctionTypeId).HasMasterId().IsRequired();
    builder.Property(x => x.AuctionStatusId).HasMasterId().IsRequired();
    builder.Property(x => x.BaseReservePrice).HasPrecision(18, 2);
    builder.Property(x => x.WinningBidAmount).HasPrecision(18, 2);
    builder.Property(x => x.SuccessfulBidderOwnerId).HasConversion(id => id!.Value, value => OwnerId.Of(value));
    builder.Property(x => x.AwardReferenceNo).HasMaxLength(100);
    builder.Property(x => x.AuctionCommitteeRef).HasMaxLength(100);
    builder.Property(x => x.Venue).HasMaxLength(200);
    builder.Property(x => x.ReferenceNo).HasMaxLength(100);

    builder.HasIndex(x => x.AuctionNo).IsUnique();
    builder.HasIndex(x => new { x.PropertyId, x.AuctionStatusId });

    builder.HasOne<Property>().WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<AuctionType>().WithMany().HasForeignKey(x => x.AuctionTypeId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<AuctionStatus>().WithMany().HasForeignKey(x => x.AuctionStatusId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<PropertyOwner>().WithMany().HasForeignKey(x => x.SuccessfulBidderOwnerId).OnDelete(DeleteBehavior.Restrict);

    builder.HasMany(x => x.Bids).WithOne().HasForeignKey(b => b.AuctionId).OnDelete(DeleteBehavior.Restrict);
    builder.Navigation(x => x.Bids).UsePropertyAccessMode(PropertyAccessMode.Field);
  }
}

/// auction_bid — the ERD gives it created_at only.
public class AuctionBidConfiguration : EntityConfiguration<AuctionBid, AuctionBidId>
{
  protected override AuditColumns Audit => AuditColumns.CreatedAt;

  public override void Configure(EntityTypeBuilder<AuctionBid> builder)
  {
    base.Configure(builder);
    builder.ToTable("auction_bid");

    builder.HasKey(x => x.Id);
    builder.Property(x => x.Id).HasConversion(id => id.Value, value => AuctionBidId.Of(value)).ValueGeneratedNever();
    builder.Property(x => x.AuctionId).HasConversion(id => id.Value, value => AuctionId.Of(value)).IsRequired();
    builder.Property(x => x.BidderOwnerId).HasConversion(id => id.Value, value => OwnerId.Of(value)).IsRequired();
    builder.Property(x => x.BidAmount).HasPrecision(18, 2).IsRequired();
    builder.Property(x => x.EarnestMoney).HasPrecision(18, 2);
    builder.Property(x => x.IsWinning).IsRequired().HasDefaultValue(false);
    builder.Property(x => x.Remarks).HasMaxLength(300);

    builder.HasOne<PropertyOwner>().WithMany().HasForeignKey(x => x.BidderOwnerId).OnDelete(DeleteBehavior.Restrict);
  }
}

/// property_outsourcing
public class PropertyOutsourcingConfiguration : EntityConfiguration<PropertyOutsourcing, OutsourcingId>
{
  public override void Configure(EntityTypeBuilder<PropertyOutsourcing> builder)
  {
    base.Configure(builder);
    builder.ToTable("property_outsourcing");

    builder.HasKey(x => x.Id);
    builder.Property(x => x.Id).HasConversion(id => id.Value, value => OutsourcingId.Of(value));
    builder.Property(x => x.PropertyId).HasConversion(id => id.Value, value => PropertyId.Of(value)).IsRequired();
    builder.Property(x => x.OutsourcedPartyOwnerId).HasConversion(id => id.Value, value => OwnerId.Of(value)).IsRequired();
    builder.Property(x => x.ContractNo).HasBusinessCode(50).IsRequired();
    builder.Property(x => x.OutsourcingTypeId).HasMasterId().IsRequired();
    builder.Property(x => x.ContractStatusId).HasMasterId().IsRequired();
    builder.Property(x => x.ContractStartDate).IsRequired();
    builder.Property(x => x.PurposeService).HasMaxLength(300);
    builder.Property(x => x.ContractAmount).HasPrecision(18, 2);
    builder.Property(x => x.AmountFrequency).HasNullableUpperSnakeEnum(20);
    builder.Property(x => x.PerformanceGuarantee).HasPrecision(18, 2);
    builder.Property(x => x.ReferenceNo).HasMaxLength(100);
    builder.Property(x => x.TerminationReason);

    builder.HasIndex(x => x.ContractNo).IsUnique();
    builder.HasIndex(x => new { x.PropertyId, x.ContractStatusId });

    builder.HasOne<Property>().WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<PropertyOwner>().WithMany().HasForeignKey(x => x.OutsourcedPartyOwnerId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<OutsourcingType>().WithMany().HasForeignKey(x => x.OutsourcingTypeId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<ContractStatus>().WithMany().HasForeignKey(x => x.ContractStatusId).OnDelete(DeleteBehavior.Restrict);
  }
}
