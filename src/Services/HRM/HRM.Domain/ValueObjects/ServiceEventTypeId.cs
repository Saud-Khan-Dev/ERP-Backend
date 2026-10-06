public sealed record ServiceEventTypeId : ITypedId<ServiceEventTypeId>
{
  public Guid Value { get; }

  private ServiceEventTypeId(Guid value) => Value = value;

  public static ServiceEventTypeId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Service event type id cannot be empty.");

    return new ServiceEventTypeId(value);
  }

  public static ServiceEventTypeId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
