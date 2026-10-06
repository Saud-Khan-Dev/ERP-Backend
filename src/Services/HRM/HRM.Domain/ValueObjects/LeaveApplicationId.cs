public sealed record LeaveApplicationId : ITypedId<LeaveApplicationId>
{
  public Guid Value { get; }

  private LeaveApplicationId(Guid value) => Value = value;

  public static LeaveApplicationId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Leave application id cannot be empty.");

    return new LeaveApplicationId(value);
  }

  public static LeaveApplicationId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
