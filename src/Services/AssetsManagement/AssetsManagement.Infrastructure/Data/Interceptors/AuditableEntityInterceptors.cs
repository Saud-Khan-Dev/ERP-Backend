using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

/// Stamps who created and last changed every row - the signed-in user from the token ("system" for background work).
public class AuditableEntityInterceptors(ICurrentUser currentUser) : SaveChangesInterceptor
{
  public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
  {
    UpdateEntities(eventData.Context);
    return base.SavingChanges(eventData, result);
  }

  public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
  {
    UpdateEntities(eventData.Context);
    return base.SavingChangesAsync(eventData, result, cancellationToken);
  }

  private void UpdateEntities(DbContext? context)
  {
    if (context == null)
      return;

    var actor = currentUser.AuditName;
    var now = DateTime.UtcNow;

    foreach (var entry in context.ChangeTracker.Entries<IEntity>())
    {
      if (entry.State == EntityState.Added)
      {
        entry.Entity.CreatedBy = actor;
        entry.Entity.CreatedAt = now;
      }

      if (entry.State == EntityState.Added || entry.State == EntityState.Modified)
      {
        entry.Entity.LastModifiedBy = actor;
        entry.Entity.LastModified = now;
      }
    }
  }
}

public static class Extensions
{
  public static bool HasChangedOwnedEntities(this EntityEntry entityEntry)
  {
    return entityEntry.References.Any(r =>
      r.TargetEntry != null &&
      r.TargetEntry.Metadata.IsOwned()
      && (r.TargetEntry.State == EntityState.Added || r.TargetEntry.State == EntityState.Modified));
  }
}
