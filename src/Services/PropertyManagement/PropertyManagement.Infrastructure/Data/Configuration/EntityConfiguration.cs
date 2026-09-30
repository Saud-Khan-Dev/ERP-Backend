using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// Which audit columns a table has in the ERD. Business tables carry all four; some detail tables carry
/// fewer (auction_bid: created_at only) or none (boundary_point).
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

/// Maps the audit columns a table has; the ones it does not have in the ERD are left unmapped.
public abstract class EntityConfiguration<TEntity, TId> : IEntityTypeConfiguration<TEntity>
  where TEntity : Entity<TId>
{
  protected virtual AuditColumns Audit => AuditColumns.All;

  public virtual void Configure(EntityTypeBuilder<TEntity> builder)
  {
    if (Audit.HasFlag(AuditColumns.CreatedAt)) builder.Property(x => x.CreatedAt).IsRequired(false);
    else builder.Ignore(x => x.CreatedAt);

    if (Audit.HasFlag(AuditColumns.CreatedBy)) builder.Property(x => x.CreatedBy).IsRequired(false);
    else builder.Ignore(x => x.CreatedBy);

    if (Audit.HasFlag(AuditColumns.UpdatedAt)) builder.Property(x => x.UpdatedAt).IsRequired(false);
    else builder.Ignore(x => x.UpdatedAt);

    if (Audit.HasFlag(AuditColumns.UpdatedBy)) builder.Property(x => x.UpdatedBy).IsRequired(false);
    else builder.Ignore(x => x.UpdatedBy);
  }
}
