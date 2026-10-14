public sealed record LtfFactorId : ITypedId<LtfFactorId>
{
  public Guid Value { get; }

  private LtfFactorId(Guid value) => Value = value;

  public static LtfFactorId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Locality factor id cannot be empty.");

    return new LtfFactorId(value);
  }

  public static LtfFactorId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
