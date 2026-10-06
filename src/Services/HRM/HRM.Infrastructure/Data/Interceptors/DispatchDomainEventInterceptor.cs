using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

public class DispatchDomainEventInterceptor(IMediator mediator) : SaveChangesInterceptor
{
  public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
  {
    DispatchDomainEvents(eventData.Context).GetAwaiter().GetResult();
    return base.SavingChanges(eventData, result);
  }

  public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
  {
    await DispatchDomainEvents(eventData.Context);
    return await base.SavingChangesAsync(eventData, result, cancellationToken);
  }

  private async Task DispatchDomainEvents(DbContext? context)
  {
    if (context == null)
      return;

    var aggregates = context.ChangeTracker.Entries<IAggregate>()
      .Where(a => a.Entity.DomainEvents.Any())
      .Select(e => e.Entity)
      .ToList();

    var domainEvents = aggregates.SelectMany(e => e.DomainEvents).ToList();

    aggregates.ForEach(a => a.ClearDomainEvents());

    foreach (var domainEvent in domainEvents)
    {
      await mediator.Publish(domainEvent);
    }
  }
}
