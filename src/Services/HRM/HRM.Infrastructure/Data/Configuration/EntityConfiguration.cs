using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// Which audit columns a table has in the schema. Most tables carry only some of them (many have none).
[Flags]
public enum AuditColumns
{
  None = 0,
  CreatedAt = 1,
  CreatedBy = 2,
  UpdatedAt = 4,
  UpdatedBy = 8,
  Created = CreatedAt | CreatedBy,
  All = CreatedAt | CreatedBy | UpdatedAt | UpdatedBy
}

/// Maps the key (uuid, default gen_random_uuid()) and the audit columns a table has; the ones it does not have are left
/// unmapped. created_at is NOT NULL DEFAULT now() wherever it exists.
public abstract class EntityConfiguration<TEntity, TId> : IEntityTypeConfiguration<TEntity>
  where TEntity : Entity<TId>
{
  protected virtual AuditColumns Audit => AuditColumns.All;

  public virtual void Configure(EntityTypeBuilder<TEntity> builder)
  {
    builder.HasKey(x => x.Id);
    builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");

    if (Audit.HasFlag(AuditColumns.CreatedAt)) builder.Property(x => x.CreatedAt).IsRequired().HasDefaultValueSql("now()");
    else builder.Ignore(x => x.CreatedAt);

    if (Audit.HasFlag(AuditColumns.CreatedBy)) builder.Property(x => x.CreatedBy).IsRequired(false);
    else builder.Ignore(x => x.CreatedBy);

    if (Audit.HasFlag(AuditColumns.UpdatedAt)) builder.Property(x => x.UpdatedAt).IsRequired(false);
    else builder.Ignore(x => x.UpdatedAt);

    if (Audit.HasFlag(AuditColumns.UpdatedBy)) builder.Property(x => x.UpdatedBy).IsRequired(false);
    else builder.Ignore(x => x.UpdatedBy);
  }
}
