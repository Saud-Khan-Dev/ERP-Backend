public class Aggregate<TId> : Entity<TId>, IAggregate<TId>
{
  private List<IDomainEvent> _domainEvents = new();
  public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

  public void AddDomainEvent(IDomainEvent domainEvent)
  {
    _domainEvents.Add(domainEvent);
  }

  public IDomainEvent[] ClearDomainEvents()
  {
    var dequeuedEvents = _domainEvents.ToArray();
    _domainEvents.Clear();
    return dequeuedEvents;
  }
}
