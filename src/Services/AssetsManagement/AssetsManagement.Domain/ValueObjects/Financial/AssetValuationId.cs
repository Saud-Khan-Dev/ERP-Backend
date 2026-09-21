public sealed record AssetValuationId
{
  public Guid Value { get; }

  private AssetValuationId(Guid value) => Value = value;

  public static AssetValuationId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Asset valuation Id cannot be empty");

    return new AssetValuationId(value);
  }
}
