public sealed record SmsMessageId : ITypedId<SmsMessageId>
{
  public Guid Value { get; }

  private SmsMessageId(Guid value) => Value = value;

  public static SmsMessageId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("SMS message id cannot be empty.");

    return new SmsMessageId(value);
  }

  public static SmsMessageId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
