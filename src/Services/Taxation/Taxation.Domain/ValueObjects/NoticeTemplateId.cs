public sealed record NoticeTemplateId : ITypedId<NoticeTemplateId>
{
  public Guid Value { get; }

  private NoticeTemplateId(Guid value) => Value = value;

  public static NoticeTemplateId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Notice template id cannot be empty.");

    return new NoticeTemplateId(value);
  }

  public static NoticeTemplateId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
