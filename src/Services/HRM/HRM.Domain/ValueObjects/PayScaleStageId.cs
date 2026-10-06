public sealed record PayScaleStageId : ITypedId<PayScaleStageId>
{
  public Guid Value { get; }

  private PayScaleStageId(Guid value) => Value = value;

  public static PayScaleStageId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Pay scale stage id cannot be empty.");

    return new PayScaleStageId(value);
  }

  public static PayScaleStageId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
