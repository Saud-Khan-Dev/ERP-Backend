public sealed record DocumentTypeId : ITypedId<DocumentTypeId>
{
  public Guid Value { get; }

  private DocumentTypeId(Guid value) => Value = value;

  public static DocumentTypeId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Document type id cannot be empty.");

    return new DocumentTypeId(value);
  }

  public static DocumentTypeId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
