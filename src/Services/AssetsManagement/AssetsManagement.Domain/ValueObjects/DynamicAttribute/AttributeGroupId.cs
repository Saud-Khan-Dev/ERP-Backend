public sealed record AttributeGroupId
{
  public Guid Value { get; }

  private AttributeGroupId(Guid value) => Value = value;

  public static AttributeGroupId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Attribute group Id cannot be empty");

    return new AttributeGroupId(value);
  }
}
