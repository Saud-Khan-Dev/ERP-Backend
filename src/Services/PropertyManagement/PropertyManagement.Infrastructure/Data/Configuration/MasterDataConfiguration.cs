using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// The shape every master table shares: id, code (unique), name, description, sort_order, is_active —
/// plus the few extra columns of measurement_unit, document_type and transfer_type.
public sealed class MasterDataConfiguration<T>(string table) : EntityConfiguration<T, MasterId>
  where T : MasterData
{
  /// Master tables carry no audit columns in the ERD, except town (created_at / updated_at).
  protected override AuditColumns Audit =>
      typeof(T) == typeof(Town) ? AuditColumns.CreatedAt | AuditColumns.UpdatedAt : AuditColumns.None;

  public override void Configure(EntityTypeBuilder<T> builder)
  {
    base.Configure(builder);

    builder.ToTable(table);

    builder.HasKey(x => x.Id);
    builder.Property(x => x.Id).HasMasterId();

    // column sizes per the ERD: code varchar(30) / name varchar(100) unless MasterLimits says otherwise
    var limits = MasterLimits.For(typeof(T));

    builder.Property(x => x.Code)
      .HasConversion(code => code.Value, value => MasterCode.Of(value))
      .HasMaxLength(limits.CodeLength)
      .IsRequired();

    builder.Property(x => x.Name)
      .HasConversion(name => name.Value, value => Name.Of(value, MasterData.NameMaxLength))
      .HasMaxLength(limits.NameLength)
      .IsRequired();

    builder.Property(x => x.Description).IsRequired(false);
    builder.Property(x => x.SortOrder).IsRequired().HasDefaultValue(0);
    builder.Property(x => x.IsActive).IsRequired().HasDefaultValue(true);

    builder.HasIndex(x => x.Code).IsUnique();

    switch (builder)
    {
      case EntityTypeBuilder<MeasurementUnit> unit:
        unit.Property(x => x.FactorToBase).HasPrecision(18, 8).IsRequired();
        unit.Property(x => x.IsBase).IsRequired().HasDefaultValue(false);
        // only one base unit (square feet)
        unit.HasIndex(x => x.IsBase).IsUnique().HasFilter("is_base").HasDatabaseName("ux_measurement_unit_single_base");
        unit.ToTable(t => t.HasCheckConstraint("ck_measurement_unit_factor", "factor_to_base > 0"));
        break;

      case EntityTypeBuilder<DocumentType> documentType:
        documentType.Property(x => x.StorageFolder).HasMaxLength(DocumentType.StorageFolderMaxLength).IsRequired();
        break;

      case EntityTypeBuilder<TransferType> transferType:
        transferType.Property(x => x.RequiresRelationship).IsRequired().HasDefaultValue(false);
        break;
    }
  }
}

public static class MasterDataConfiguration
{
  private static readonly System.Reflection.MethodInfo ApplyMethod = typeof(ModelBuilder).GetMethods()
      .Single(m => m.Name == nameof(ModelBuilder.ApplyConfiguration)
                   && m.GetParameters()[0].ParameterType.GetGenericTypeDefinition() == typeof(IEntityTypeConfiguration<>));

  /// ApplyConfigurationsFromAssembly skips generic configurations, so each master is applied here.
  public static void Apply(ModelBuilder builder, MasterDescriptor descriptor)
  {
    var configuration = Activator.CreateInstance(typeof(MasterDataConfiguration<>).MakeGenericType(descriptor.ClrType), descriptor.Table)!;
    ApplyMethod.MakeGenericMethod(descriptor.ClrType).Invoke(builder, new[] { configuration });
  }
}
