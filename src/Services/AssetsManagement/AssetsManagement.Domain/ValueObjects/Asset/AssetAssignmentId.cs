public sealed record AssetAssignmentId
{
  public Guid Value { get; }

  private AssetAssignmentId(Guid value) => Value = value;

  public static AssetAssignmentId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Asset assignment Id cannot be empty");

    return new AssetAssignmentId(value);
  }
}
