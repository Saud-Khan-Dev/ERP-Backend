public sealed record ViolationId
{
  public Guid Value { get; }

  private ViolationId(Guid value) => Value = value;

  public static ViolationId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Agreement violation id cannot be empty.");

    return new ViolationId(value);
  }

  public static ViolationId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
