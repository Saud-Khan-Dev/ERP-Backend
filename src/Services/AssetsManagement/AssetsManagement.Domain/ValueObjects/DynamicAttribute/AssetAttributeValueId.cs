public sealed record AssetAttributeValueId
{
  public Guid Value { get; }

  private AssetAttributeValueId(Guid value) => Value = value;

  public static AssetAttributeValueId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Asset attribute value Id cannot be empty");

    return new AssetAttributeValueId(value);
  }
}
