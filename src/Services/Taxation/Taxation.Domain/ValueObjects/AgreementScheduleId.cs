public sealed record AgreementScheduleId : ITypedId<AgreementScheduleId>
{
  public Guid Value { get; }

  private AgreementScheduleId(Guid value) => Value = value;

  public static AgreementScheduleId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Agreement id cannot be empty.");

    return new AgreementScheduleId(value);
  }

  public static AgreementScheduleId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
