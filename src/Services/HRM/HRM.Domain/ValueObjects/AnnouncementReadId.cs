public sealed record AnnouncementReadId : ITypedId<AnnouncementReadId>
{
  public Guid Value { get; }

  private AnnouncementReadId(Guid value) => Value = value;

  public static AnnouncementReadId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Announcement id cannot be empty.");

    return new AnnouncementReadId(value);
  }

  public static AnnouncementReadId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
