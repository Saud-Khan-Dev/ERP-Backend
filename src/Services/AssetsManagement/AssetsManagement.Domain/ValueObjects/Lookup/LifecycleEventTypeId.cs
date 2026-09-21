public sealed record LifecycleEventTypeId
{
  public Guid Value { get; }

  private LifecycleEventTypeId(Guid value) => Value = value;

  public static LifecycleEventTypeId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Lifecycle event type Id cannot be empty");

    return new LifecycleEventTypeId(value);
  }
}
