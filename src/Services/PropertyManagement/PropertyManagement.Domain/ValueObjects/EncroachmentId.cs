public sealed record EncroachmentId
{
  public Guid Value { get; }

  private EncroachmentId(Guid value) => Value = value;

  public static EncroachmentId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Encroachment id cannot be empty.");

    return new EncroachmentId(value);
  }

  public static EncroachmentId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
