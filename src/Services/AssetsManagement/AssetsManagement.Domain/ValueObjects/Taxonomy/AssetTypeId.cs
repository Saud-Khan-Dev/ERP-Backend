public sealed record AssetTypeId
{
  public Guid Value { get; }

  private AssetTypeId(Guid value) => Value = value;

  public static AssetTypeId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Asset type Id cannot be empty");

    return new AssetTypeId(value);
  }
}
