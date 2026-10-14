public sealed record NoticeLetterheadId : ITypedId<NoticeLetterheadId>
{
  public Guid Value { get; }

  private NoticeLetterheadId(Guid value) => Value = value;

  public static NoticeLetterheadId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Letterhead id cannot be empty.");

    return new NoticeLetterheadId(value);
  }

  public static NoticeLetterheadId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
