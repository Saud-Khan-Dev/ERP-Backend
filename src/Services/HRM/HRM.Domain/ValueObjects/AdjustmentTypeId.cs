public sealed record AdjustmentTypeId : ITypedId<AdjustmentTypeId>
{
  public Guid Value { get; }

  private AdjustmentTypeId(Guid value) => Value = value;

  public static AdjustmentTypeId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Adjustment type id cannot be empty.");

    return new AdjustmentTypeId(value);
  }

  public static AdjustmentTypeId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
