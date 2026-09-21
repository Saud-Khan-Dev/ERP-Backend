public sealed record AssetDepreciationEntryId
{
  public Guid Value { get; }

  private AssetDepreciationEntryId(Guid value) => Value = value;

  public static AssetDepreciationEntryId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Depreciation entry Id cannot be empty");

    return new AssetDepreciationEntryId(value);
  }
}
