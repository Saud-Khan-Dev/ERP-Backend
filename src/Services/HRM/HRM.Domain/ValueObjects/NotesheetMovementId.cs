public sealed record NotesheetMovementId : ITypedId<NotesheetMovementId>
{
  public Guid Value { get; }

  private NotesheetMovementId(Guid value) => Value = value;

  public static NotesheetMovementId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Notesheet id cannot be empty.");

    return new NotesheetMovementId(value);
  }

  public static NotesheetMovementId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
