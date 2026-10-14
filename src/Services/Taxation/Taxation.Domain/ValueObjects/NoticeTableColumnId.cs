public sealed record NoticeTableColumnId : ITypedId<NoticeTableColumnId>
{
  public Guid Value { get; }

  private NoticeTableColumnId(Guid value) => Value = value;

  public static NoticeTableColumnId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Notice table column id cannot be empty.");

    return new NoticeTableColumnId(value);
  }

  public static NoticeTableColumnId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
