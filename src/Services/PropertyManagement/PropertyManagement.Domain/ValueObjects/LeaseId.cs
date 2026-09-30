public sealed record LeaseId
{
  public Guid Value { get; }

  private LeaseId(Guid value) => Value = value;

  public static LeaseId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Lease id cannot be empty.");

    return new LeaseId(value);
  }

  public static LeaseId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
