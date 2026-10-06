public sealed record EmployeeDocumentId : ITypedId<EmployeeDocumentId>
{
  public Guid Value { get; }

  private EmployeeDocumentId(Guid value) => Value = value;

  public static EmployeeDocumentId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Document id cannot be empty.");

    return new EmployeeDocumentId(value);
  }

  public static EmployeeDocumentId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
