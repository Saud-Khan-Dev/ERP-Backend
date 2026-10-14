public sealed record ArrearsId : ITypedId<ArrearsId>
{
  public Guid Value { get; }

  private ArrearsId(Guid value) => Value = value;

  public static ArrearsId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Arrears row id cannot be empty.");

    return new ArrearsId(value);
  }

  public static ArrearsId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
