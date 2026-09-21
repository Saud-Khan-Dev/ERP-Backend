public sealed record AssetCategoryId
{
  public Guid Value { get; }

  private AssetCategoryId(Guid value) => Value = value;

  public static AssetCategoryId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Asset category Id cannot be empty");

    return new AssetCategoryId(value);
  }
}
