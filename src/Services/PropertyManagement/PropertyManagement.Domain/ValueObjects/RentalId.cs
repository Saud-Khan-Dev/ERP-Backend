public sealed record RentalId
{
  public Guid Value { get; }

  private RentalId(Guid value) => Value = value;

  public static RentalId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Rental id cannot be empty.");

    return new RentalId(value);
  }

  public static RentalId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
