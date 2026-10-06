public sealed record EmergencyContactId : ITypedId<EmergencyContactId>
{
  public Guid Value { get; }

  private EmergencyContactId(Guid value) => Value = value;

  public static EmergencyContactId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Emergency contact id cannot be empty.");

    return new EmergencyContactId(value);
  }

  public static EmergencyContactId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
