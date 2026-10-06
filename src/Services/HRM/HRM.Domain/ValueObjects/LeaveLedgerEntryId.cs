public sealed record LeaveLedgerEntryId : ITypedId<LeaveLedgerEntryId>
{
  public Guid Value { get; }

  private LeaveLedgerEntryId(Guid value) => Value = value;

  public static LeaveLedgerEntryId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Leave ledger entry id cannot be empty.");

    return new LeaveLedgerEntryId(value);
  }

  public static LeaveLedgerEntryId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
