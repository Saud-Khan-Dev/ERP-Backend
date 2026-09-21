public sealed record AssetAcquisitionId
{
  public Guid Value { get; }

  private AssetAcquisitionId(Guid value) => Value = value;

  public static AssetAcquisitionId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Asset acquisition Id cannot be empty");

    return new AssetAcquisitionId(value);
  }
}
