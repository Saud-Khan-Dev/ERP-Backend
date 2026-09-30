public sealed record AreaRegularizationId
{
  public Guid Value { get; }

  private AreaRegularizationId(Guid value) => Value = value;

  public static AreaRegularizationId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Area regularization id cannot be empty.");

    return new AreaRegularizationId(value);
  }

  public static AreaRegularizationId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
