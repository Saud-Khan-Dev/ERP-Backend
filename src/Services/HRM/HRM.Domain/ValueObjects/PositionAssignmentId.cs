public sealed record PositionAssignmentId : ITypedId<PositionAssignmentId>
{
  public Guid Value { get; }

  private PositionAssignmentId(Guid value) => Value = value;

  public static PositionAssignmentId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Position assignment id cannot be empty.");

    return new PositionAssignmentId(value);
  }

  public static PositionAssignmentId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
