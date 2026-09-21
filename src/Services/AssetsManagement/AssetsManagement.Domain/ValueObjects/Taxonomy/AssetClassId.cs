public sealed record AssetClassId
{
  public Guid Value { get; }

  private AssetClassId(Guid value) => Value = value;

  public static AssetClassId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Asset class Id cannot be empty");

    return new AssetClassId(value);
  }
}
