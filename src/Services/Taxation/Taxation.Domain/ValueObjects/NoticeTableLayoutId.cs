public sealed record NoticeTableLayoutId : ITypedId<NoticeTableLayoutId>
{
  public Guid Value { get; }

  private NoticeTableLayoutId(Guid value) => Value = value;

  public static NoticeTableLayoutId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Notice table layout id cannot be empty.");

    return new NoticeTableLayoutId(value);
  }

  public static NoticeTableLayoutId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
