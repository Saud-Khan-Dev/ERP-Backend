public sealed record PropertyAttributeValueId
{
  public Guid Value { get; }

  private PropertyAttributeValueId(Guid value) => Value = value;

  public static PropertyAttributeValueId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Property attribute value id cannot be empty.");

    return new PropertyAttributeValueId(value);
  }

  public static PropertyAttributeValueId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
