public sealed record EncroachmentPointId
{
  public Guid Value { get; }

  private EncroachmentPointId(Guid value) => Value = value;

  public static EncroachmentPointId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Encroachment boundary point id cannot be empty.");

    return new EncroachmentPointId(value);
  }

  public static EncroachmentPointId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
