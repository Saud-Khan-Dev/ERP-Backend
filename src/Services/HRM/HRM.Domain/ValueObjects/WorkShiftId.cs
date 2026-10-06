public sealed record WorkShiftId : ITypedId<WorkShiftId>
{
  public Guid Value { get; }

  private WorkShiftId(Guid value) => Value = value;

  public static WorkShiftId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Work shift id cannot be empty.");

    return new WorkShiftId(value);
  }

  public static WorkShiftId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
