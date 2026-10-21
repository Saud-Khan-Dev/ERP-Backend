public sealed record EmployeeChangeLogEntryId : ITypedId<EmployeeChangeLogEntryId>
{
  public Guid Value { get; }

  private EmployeeChangeLogEntryId(Guid value) => Value = value;

  public static EmployeeChangeLogEntryId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Change log entry id cannot be empty.");

    return new EmployeeChangeLogEntryId(value);
  }

  public static EmployeeChangeLogEntryId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
