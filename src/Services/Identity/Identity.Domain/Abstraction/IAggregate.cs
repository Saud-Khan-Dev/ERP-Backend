public interface IAggregate<T> : IAggregate, IEntity<T>
{
}

/// Note: unlike the older services, an aggregate is not itself an IDomainEvent — that made it
/// possible to accidentally publish the aggregate instead of an event.
public interface IAggregate : IEntity
{
  IReadOnlyList<IDomainEvent> DomainEvents { get; }
  IDomainEvent[] ClearDomainEvents();
}
