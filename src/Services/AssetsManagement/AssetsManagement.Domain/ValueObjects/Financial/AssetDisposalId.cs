public sealed record AssetDisposalId
{
  public Guid Value { get; }

  private AssetDisposalId(Guid value) => Value = value;

  public static AssetDisposalId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Asset disposal Id cannot be empty");

    return new AssetDisposalId(value);
  }
}
