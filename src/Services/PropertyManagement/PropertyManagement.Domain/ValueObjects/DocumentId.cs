public sealed record DocumentId
{
  public Guid Value { get; }

  private DocumentId(Guid value) => Value = value;

  public static DocumentId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Document id cannot be empty.");

    return new DocumentId(value);
  }

  public static DocumentId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
