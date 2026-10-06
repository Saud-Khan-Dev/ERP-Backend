public sealed record TaxLedgerEntryId : ITypedId<TaxLedgerEntryId>
{
  public Guid Value { get; }

  private TaxLedgerEntryId(Guid value) => Value = value;

  public static TaxLedgerEntryId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Tax ledger entry id cannot be empty.");

    return new TaxLedgerEntryId(value);
  }

  public static TaxLedgerEntryId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
