public sealed record AssetStatusId
{
  public Guid Value { get; }

  private AssetStatusId(Guid value) => Value = value;

  public static AssetStatusId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Asset status Id cannot be empty");

    return new AssetStatusId(value);
  }
}
