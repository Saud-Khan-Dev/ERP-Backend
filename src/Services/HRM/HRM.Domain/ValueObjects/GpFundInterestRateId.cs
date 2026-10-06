public sealed record GpFundInterestRateId : ITypedId<GpFundInterestRateId>
{
  public Guid Value { get; }

  private GpFundInterestRateId(Guid value) => Value = value;

  public static GpFundInterestRateId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("GP Fund interest rate id cannot be empty.");

    return new GpFundInterestRateId(value);
  }

  public static GpFundInterestRateId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
