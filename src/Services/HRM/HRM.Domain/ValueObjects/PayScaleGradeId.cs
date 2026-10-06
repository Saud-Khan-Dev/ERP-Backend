public sealed record PayScaleGradeId : ITypedId<PayScaleGradeId>
{
  public Guid Value { get; }

  private PayScaleGradeId(Guid value) => Value = value;

  public static PayScaleGradeId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Pay scale grade id cannot be empty.");

    return new PayScaleGradeId(value);
  }

  public static PayScaleGradeId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
