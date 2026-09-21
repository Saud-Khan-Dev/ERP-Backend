public sealed record AssetAttachmentId
{
  public Guid Value { get; }

  private AssetAttachmentId(Guid value) => Value = value;

  public static AssetAttachmentId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Asset attachment Id cannot be empty");

    return new AssetAttachmentId(value);
  }
}
