public sealed record ServiceHistoryId : ITypedId<ServiceHistoryId>
{
  public Guid Value { get; }

  private ServiceHistoryId(Guid value) => Value = value;

  public static ServiceHistoryId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Service history entry id cannot be empty.");

    return new ServiceHistoryId(value);
  }

  public static ServiceHistoryId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
