public sealed record EngagementOrderId : ITypedId<EngagementOrderId>
{
  public Guid Value { get; }

  private EngagementOrderId(Guid value) => Value = value;

  public static EngagementOrderId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Order id cannot be empty.");

    return new EngagementOrderId(value);
  }

  public static EngagementOrderId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
