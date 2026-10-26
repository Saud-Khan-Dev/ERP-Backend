public sealed record HistoryEntryId
{
  public Guid Value { get; }

  private HistoryEntryId(Guid value) => Value = value;

  public static HistoryEntryId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Ownership history entry id cannot be empty.");

    return new HistoryEntryId(value);
  }

  public static HistoryEntryId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
