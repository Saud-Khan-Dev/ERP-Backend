public sealed record SeparationId : ITypedId<SeparationId>
{
  public Guid Value { get; }

  private SeparationId(Guid value) => Value = value;

  public static SeparationId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Separation id cannot be empty.");

    return new SeparationId(value);
  }

  public static SeparationId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
