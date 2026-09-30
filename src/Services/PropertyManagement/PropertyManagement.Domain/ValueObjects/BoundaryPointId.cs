public sealed record BoundaryPointId
{
  public Guid Value { get; }

  private BoundaryPointId(Guid value) => Value = value;

  public static BoundaryPointId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Boundary point id cannot be empty.");

    return new BoundaryPointId(value);
  }

  public static BoundaryPointId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
