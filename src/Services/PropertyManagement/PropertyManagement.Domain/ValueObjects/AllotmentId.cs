public sealed record AllotmentId
{
  public Guid Value { get; }

  private AllotmentId(Guid value) => Value = value;

  public static AllotmentId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Allotment id cannot be empty.");

    return new AllotmentId(value);
  }

  public static AllotmentId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
