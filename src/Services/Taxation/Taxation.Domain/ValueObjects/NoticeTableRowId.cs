public sealed record NoticeTableRowId : ITypedId<NoticeTableRowId>
{
  public Guid Value { get; }

  private NoticeTableRowId(Guid value) => Value = value;

  public static NoticeTableRowId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Notice table row id cannot be empty.");

    return new NoticeTableRowId(value);
  }

  public static NoticeTableRowId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
