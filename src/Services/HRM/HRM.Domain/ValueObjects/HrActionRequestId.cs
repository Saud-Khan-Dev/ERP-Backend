public sealed record HrActionRequestId : ITypedId<HrActionRequestId>
{
  public Guid Value { get; }

  private HrActionRequestId(Guid value) => Value = value;

  public static HrActionRequestId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("HR action id cannot be empty.");

    return new HrActionRequestId(value);
  }

  public static HrActionRequestId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
