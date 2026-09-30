public sealed record PropertyStatusHistoryId
{
  public Guid Value { get; }

  private PropertyStatusHistoryId(Guid value) => Value = value;

  public static PropertyStatusHistoryId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Property status history id cannot be empty.");

    return new PropertyStatusHistoryId(value);
  }

  public static PropertyStatusHistoryId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
