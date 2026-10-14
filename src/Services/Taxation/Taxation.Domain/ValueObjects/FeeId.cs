public sealed record FeeId : ITypedId<FeeId>
{
  public Guid Value { get; }

  private FeeId(Guid value) => Value = value;

  public static FeeId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Fee id cannot be empty.");

    return new FeeId(value);
  }

  public static FeeId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
