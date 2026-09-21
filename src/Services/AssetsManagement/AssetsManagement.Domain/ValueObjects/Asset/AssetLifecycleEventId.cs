public sealed record AssetLifecycleEventId
{
  public Guid Value { get; }

  private AssetLifecycleEventId(Guid value) => Value = value;

  public static AssetLifecycleEventId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Asset lifecycle event Id cannot be empty");

    return new AssetLifecycleEventId(value);
  }
}
