public sealed record MessageTemplateId : ITypedId<MessageTemplateId>
{
  public Guid Value { get; }

  private MessageTemplateId(Guid value) => Value = value;

  public static MessageTemplateId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Message template id cannot be empty.");

    return new MessageTemplateId(value);
  }

  public static MessageTemplateId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
