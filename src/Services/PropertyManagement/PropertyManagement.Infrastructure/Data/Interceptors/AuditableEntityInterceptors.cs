using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

/// Stamps created_at / created_by / updated_at / updated_by. The actor is the authenticated caller's
/// Identity user id from the access token — never a request field. Null during seeding.
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

    var actor = currentUser.UserId;
    var now = DateTime.UtcNow;

    foreach (var entry in context.ChangeTracker.Entries<IEntity>())
    {
      if (entry.State == EntityState.Added)
      {
        entry.Entity.CreatedBy = actor;
        entry.Entity.CreatedAt = now;
      }

      if (entry.State == EntityState.Modified)
      {
        entry.Entity.UpdatedBy = actor;
        entry.Entity.UpdatedAt = now;
      }
    }
  }
}
