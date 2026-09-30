public sealed record AttributeDefinitionId
{
  public Guid Value { get; }

  private AttributeDefinitionId(Guid value) => Value = value;

  public static AttributeDefinitionId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Attribute definition id cannot be empty.");

    return new AttributeDefinitionId(value);
  }

  public static AttributeDefinitionId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
