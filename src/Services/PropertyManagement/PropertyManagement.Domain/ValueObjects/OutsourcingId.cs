public sealed record OutsourcingId
{
  public Guid Value { get; }

  private OutsourcingId(Guid value) => Value = value;

  public static OutsourcingId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Outsourcing contract id cannot be empty.");

    return new OutsourcingId(value);
  }

  public static OutsourcingId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
