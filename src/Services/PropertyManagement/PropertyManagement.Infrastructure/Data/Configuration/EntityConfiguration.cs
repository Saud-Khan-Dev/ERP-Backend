using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// Audit columns every table carries.
public abstract class EntityConfiguration<TEntity, TId> : IEntityTypeConfiguration<TEntity>
  where TEntity : Entity<TId>
{
  public virtual void Configure(EntityTypeBuilder<TEntity> builder)
  {
    builder.Property(x => x.CreatedAt).IsRequired(false);
    builder.Property(x => x.CreatedBy).IsRequired(false);
    builder.Property(x => x.UpdatedAt).IsRequired(false);
    builder.Property(x => x.UpdatedBy).IsRequired(false);
  }
}
