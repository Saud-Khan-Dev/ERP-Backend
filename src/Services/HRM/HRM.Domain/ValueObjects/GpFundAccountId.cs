public sealed record GpFundAccountId : ITypedId<GpFundAccountId>
{
  public Guid Value { get; }

  private GpFundAccountId(Guid value) => Value = value;

  public static GpFundAccountId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("GP Fund account id cannot be empty.");

    return new GpFundAccountId(value);
  }

  public static GpFundAccountId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
