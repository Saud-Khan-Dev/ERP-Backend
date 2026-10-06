public sealed record BankAccountId : ITypedId<BankAccountId>
{
  public Guid Value { get; }

  private BankAccountId(Guid value) => Value = value;

  public static BankAccountId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Bank account id cannot be empty.");

    return new BankAccountId(value);
  }

  public static BankAccountId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
