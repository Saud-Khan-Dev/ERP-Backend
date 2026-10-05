public sealed record ActivityId
{
  public Guid Value { get; }

  private ActivityId(Guid value) => Value = value;

  public static ActivityId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Activity Id cannot be empty");

    return new ActivityId(value);
  }
}
