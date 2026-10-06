public sealed record LeaveTypeId : ITypedId<LeaveTypeId>
{
  public Guid Value { get; }

  private LeaveTypeId(Guid value) => Value = value;

  public static LeaveTypeId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Leave type id cannot be empty.");

    return new LeaveTypeId(value);
  }

  public static LeaveTypeId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
