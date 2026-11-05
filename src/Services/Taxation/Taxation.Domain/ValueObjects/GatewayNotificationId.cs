public sealed record GatewayNotificationId : ITypedId<GatewayNotificationId>
{
  public Guid Value { get; }

  private GatewayNotificationId(Guid value) => Value = value;

  public static GatewayNotificationId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Gateway payment id cannot be empty.");

    return new GatewayNotificationId(value);
  }

  public static GatewayNotificationId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
