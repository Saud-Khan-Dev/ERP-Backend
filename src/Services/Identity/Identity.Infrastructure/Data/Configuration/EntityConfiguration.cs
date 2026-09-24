using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public abstract class EntityConfiguration<TEntity, TId> : IEntityTypeConfiguration<TEntity>
  where TEntity : Entity<TId>
{
  public virtual void Configure(EntityTypeBuilder<TEntity> builder)
  {
    builder.Property(x => x.CreatedAt).IsRequired(false);
    builder.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired(false);
    builder.Property(x => x.LastModified).IsRequired(false);
    builder.Property(x => x.LastModifiedBy).HasMaxLength(100).IsRequired(false);
  }
}
