public sealed record NoticeId : ITypedId<NoticeId>
{
  public Guid Value { get; }

  private NoticeId(Guid value) => Value = value;

  public static NoticeId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Notice id cannot be empty.");

    return new NoticeId(value);
  }

  public static NoticeId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
