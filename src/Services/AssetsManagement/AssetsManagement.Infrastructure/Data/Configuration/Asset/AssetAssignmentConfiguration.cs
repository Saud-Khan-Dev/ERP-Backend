using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class AssetAssignmentConfiguration : EntityConfiguration<AssetAssignment, AssetAssignmentId>
{
  public override void Configure(EntityTypeBuilder<AssetAssignment> builder)
  {
    base.Configure(builder);

    builder.ToTable("asset_assignment");

    builder.HasKey(x => x.Id);
    builder.Property(x => x.Id).HasConversion(id => id.Value, dbId => AssetAssignmentId.Of(dbId));

    builder.Property(x => x.AssetId).HasConversion(id => id.Value, dbId => AssetId.Of(dbId)).IsRequired();
    builder.Property(x => x.FromDepartmentId).IsRequired(false);
    builder.Property(x => x.ToDepartmentId).IsRequired(false);
    builder.Property(x => x.FromCustodianId).IsRequired(false);
    builder.Property(x => x.ToCustodianId).IsRequired(false);

    builder.Property(x => x.FromLocationId).HasConversion(id => id!.Value, dbId => LocationId.Of(dbId)).IsRequired(false);
    builder.Property(x => x.ToLocationId).HasConversion(id => id!.Value, dbId => LocationId.Of(dbId)).IsRequired(false);

    builder.Property(x => x.AssignmentDate).IsRequired();
    builder.Property(x => x.ExpectedReturnDate).IsRequired(false);
    builder.Property(x => x.ActualReturnDate).IsRequired(false);
    builder.Property(x => x.Reason).IsRequired(false);
    builder.Property(x => x.ApprovedBy).IsRequired(false);
    builder.Property(x => x.ApprovedAt).IsRequired(false);

    builder.Ignore(x => x.IsOpenLoan);

    builder.HasIndex(x => x.AssetId);
    builder.HasIndex(x => x.AssignmentDate);
    builder.HasIndex(x => new { x.AssetId, x.AssignmentDate });

    builder.HasOne<Asset>().WithMany().HasForeignKey(x => x.AssetId).IsRequired().OnDelete(DeleteBehavior.Cascade);
    builder.HasOne<Location>().WithMany().HasForeignKey(x => x.FromLocationId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<Location>().WithMany().HasForeignKey(x => x.ToLocationId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
  }
}
