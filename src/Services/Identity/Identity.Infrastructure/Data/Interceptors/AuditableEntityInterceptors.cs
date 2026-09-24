using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

/// Stamps the audit columns with the *authenticated* caller.
///
/// This is the pattern the business services should adopt: the actor comes from the access token
/// via ICurrentUser, never from a hard-coded string or a request body field.
public class AuditableEntityInterceptors(ICurrentUser currentUser) : SaveChangesInterceptor
{
  public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
  {
    UpdateEntities(eventData.Context);
    return base.SavingChanges(eventData, result);
  }

  public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
      DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
  {
    UpdateEntities(eventData.Context);
    return base.SavingChangesAsync(eventData, result, cancellationToken);
  }

  private void UpdateEntities(DbContext? context)
  {
    if (context is null)
      return;

    // "system" during seeding and background work, where there is no HTTP caller
    var actor = currentUser.AuditName;
    var now = DateTime.UtcNow;

    foreach (var entry in context.ChangeTracker.Entries<IEntity>())
    {
      if (entry.State == EntityState.Added)
      {
        entry.Entity.CreatedBy = actor;
        entry.Entity.CreatedAt = now;
      }

      if (entry.State is EntityState.Added or EntityState.Modified)
      {
        entry.Entity.LastModifiedBy = actor;
        entry.Entity.LastModified = now;
      }
    }
  }
}
