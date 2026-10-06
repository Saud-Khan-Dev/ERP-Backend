public sealed record DesignationId : ITypedId<DesignationId>
{
  public Guid Value { get; }

  private DesignationId(Guid value) => Value = value;

  public static DesignationId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Designation id cannot be empty.");

    return new DesignationId(value);
  }

  public static DesignationId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
