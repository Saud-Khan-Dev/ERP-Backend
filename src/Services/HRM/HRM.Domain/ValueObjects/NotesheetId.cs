public sealed record NotesheetId : ITypedId<NotesheetId>
{
  public Guid Value { get; }

  private NotesheetId(Guid value) => Value = value;

  public static NotesheetId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Notesheet id cannot be empty.");

    return new NotesheetId(value);
  }

  public static NotesheetId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
