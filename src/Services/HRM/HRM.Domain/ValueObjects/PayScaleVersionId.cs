public sealed record PayScaleVersionId : ITypedId<PayScaleVersionId>
{
  public Guid Value { get; }

  private PayScaleVersionId(Guid value) => Value = value;

  public static PayScaleVersionId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Pay scale version id cannot be empty.");

    return new PayScaleVersionId(value);
  }

  public static PayScaleVersionId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
