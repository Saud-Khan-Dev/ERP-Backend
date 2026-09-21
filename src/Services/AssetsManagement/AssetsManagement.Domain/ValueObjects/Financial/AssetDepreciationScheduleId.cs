public sealed record AssetDepreciationScheduleId
{
  public Guid Value { get; }

  private AssetDepreciationScheduleId(Guid value) => Value = value;

  public static AssetDepreciationScheduleId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Depreciation schedule Id cannot be empty");

    return new AssetDepreciationScheduleId(value);
  }
}
