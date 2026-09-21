public sealed record AssetAttributeHistoryId
{
  public Guid Value { get; }

  private AssetAttributeHistoryId(Guid value) => Value = value;

  public static AssetAttributeHistoryId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Asset attribute history Id cannot be empty");

    return new AssetAttributeHistoryId(value);
  }
}
