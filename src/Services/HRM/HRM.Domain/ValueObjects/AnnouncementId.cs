public sealed record AnnouncementId : ITypedId<AnnouncementId>
{
  public Guid Value { get; }

  private AnnouncementId(Guid value) => Value = value;

  public static AnnouncementId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Announcement id cannot be empty.");

    return new AnnouncementId(value);
  }

  public static AnnouncementId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
