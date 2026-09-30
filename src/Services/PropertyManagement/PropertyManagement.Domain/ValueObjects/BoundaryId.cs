public sealed record BoundaryId
{
  public Guid Value { get; }

  private BoundaryId(Guid value) => Value = value;

  public static BoundaryId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Property boundary id cannot be empty.");

    return new BoundaryId(value);
  }

  public static BoundaryId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
