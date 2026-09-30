using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class PropertyOwnerConfiguration : EntityConfiguration<PropertyOwner, OwnerId>
{
  public override void Configure(EntityTypeBuilder<PropertyOwner> builder)
  {
    base.Configure(builder);
    builder.ToTable("property_owner");

    builder.HasKey(x => x.Id);
    builder.Property(x => x.Id).HasConversion(id => id.Value, value => OwnerId.Of(value));

    builder.Property(x => x.OwnerCode).HasBusinessCode(20).IsRequired();
    builder.Property(x => x.OwnerTypeId).HasMasterId().IsRequired();

    builder.ComplexProperty(x => x.OwnerName, name =>
      name.Property(n => n.Value).HasColumnName("owner_name").HasMaxLength(PropertyOwner.NameMaxLength).IsRequired());

    builder.Property(x => x.FatherHusbandName).HasMaxLength(200);
    builder.Property(x => x.Cnic)
      .HasConversion(c => c!.Value, value => Cnic.Of(value))
      .HasMaxLength(Cnic.MaxLength);
    builder.Property(x => x.Ntn).HasMaxLength(PropertyOwner.NtnMaxLength);
    builder.Property(x => x.RegistrationNo).HasMaxLength(50);
    builder.Property(x => x.Email)
      .HasConversion(e => e!.Value, value => EmailAddress.Of(value))
      .HasMaxLength(EmailAddress.MaxLength);
    builder.Property(x => x.CnicDocumentId).HasConversion(id => id!.Value, value => DocumentId.Of(value));
    builder.Property(x => x.IsActive).IsRequired().HasDefaultValue(true);

    builder.HasIndex(x => x.OwnerCode).IsUnique();
    builder.HasIndex(x => x.Cnic);
    builder.HasIndex(x => x.Ntn);
    // The ERD's (owner_name) index: EF cannot index a complex-type property, so the
    // PropertyManagementAndCompliance migration creates ix_property_owner_owner_name in SQL.

    builder.HasOne<OwnerType>().WithMany().HasForeignKey(x => x.OwnerTypeId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<PropertyDocument>().WithMany().HasForeignKey(x => x.CnicDocumentId).OnDelete(DeleteBehavior.Restrict);

    builder.HasMany(x => x.Contacts).WithOne().HasForeignKey(c => c.OwnerId).OnDelete(DeleteBehavior.Restrict);
    builder.HasMany(x => x.Addresses).WithOne().HasForeignKey(a => a.OwnerId).OnDelete(DeleteBehavior.Restrict);
    builder.Navigation(x => x.Contacts).UsePropertyAccessMode(PropertyAccessMode.Field);
    builder.Navigation(x => x.Addresses).UsePropertyAccessMode(PropertyAccessMode.Field);
  }
}

public class OwnerContactConfiguration : EntityConfiguration<OwnerContact, OwnerContactId>
{
  public override void Configure(EntityTypeBuilder<OwnerContact> builder)
  {
    base.Configure(builder);
    builder.ToTable("owner_contact");

    builder.HasKey(x => x.Id);
    builder.Property(x => x.Id).HasConversion(id => id.Value, value => OwnerContactId.Of(value)).ValueGeneratedNever();
    builder.Property(x => x.OwnerId).HasConversion(id => id.Value, value => OwnerId.Of(value)).IsRequired();
    builder.Property(x => x.ContactTypeId).HasMasterId().IsRequired();
    builder.Property(x => x.ContactNumber).HasMaxLength(30).IsRequired();
    builder.Property(x => x.IsPrimary).IsRequired().HasDefaultValue(false);
    builder.Property(x => x.IsActive).IsRequired().HasDefaultValue(true);
    builder.Property(x => x.Remarks).HasMaxLength(200);

    // rule 3 (one primary per owner) is kept by the PropertyOwner aggregate: moving the flag updates two
    // rows in one save, which a partial unique index could reject depending on statement order
    builder.HasIndex(x => new { x.OwnerId, x.IsPrimary });

    builder.HasOne<ContactType>().WithMany().HasForeignKey(x => x.ContactTypeId).OnDelete(DeleteBehavior.Restrict);
  }
}

public class OwnerAddressConfiguration : EntityConfiguration<OwnerAddress, OwnerAddressId>
{
  public override void Configure(EntityTypeBuilder<OwnerAddress> builder)
  {
    base.Configure(builder);
    builder.ToTable("owner_address");

    builder.HasKey(x => x.Id);
    builder.Property(x => x.Id).HasConversion(id => id.Value, value => OwnerAddressId.Of(value)).ValueGeneratedNever();
    builder.Property(x => x.OwnerId).HasConversion(id => id.Value, value => OwnerId.Of(value)).IsRequired();
    builder.Property(x => x.AddressType).HasUpperSnakeEnum(20).IsRequired();
    builder.Property(x => x.FullAddress).HasMaxLength(400).IsRequired();
    builder.Property(x => x.CityTown).HasMaxLength(100);
    builder.Property(x => x.District).HasMaxLength(100);
    builder.Property(x => x.Province).HasMaxLength(100);
    builder.Property(x => x.Country).HasMaxLength(100).IsRequired().HasDefaultValue(OwnerAddress.DefaultCountry);
    builder.Property(x => x.PostalCode).HasMaxLength(20);
    builder.Property(x => x.IsPrimary).IsRequired().HasDefaultValue(false);
    builder.Property(x => x.IsActive).IsRequired().HasDefaultValue(true);

    builder.HasIndex(x => new { x.OwnerId, x.IsPrimary });
  }
}
