public sealed record AttributeAssignmentId
{
  public Guid Value { get; }

  private AttributeAssignmentId(Guid value) => Value = value;

  public static AttributeAssignmentId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Attribute assignment Id cannot be empty");

    return new AttributeAssignmentId(value);
  }
}
