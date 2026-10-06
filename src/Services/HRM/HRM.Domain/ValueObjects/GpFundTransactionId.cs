public sealed record GpFundTransactionId : ITypedId<GpFundTransactionId>
{
  public Guid Value { get; }

  private GpFundTransactionId(Guid value) => Value = value;

  public static GpFundTransactionId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("GP Fund transaction id cannot be empty.");

    return new GpFundTransactionId(value);
  }

  public static GpFundTransactionId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
