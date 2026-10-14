public sealed record AgreementStepRevisionId : ITypedId<AgreementStepRevisionId>
{
  public Guid Value { get; }

  private AgreementStepRevisionId(Guid value) => Value = value;

  public static AgreementStepRevisionId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Step revision id cannot be empty.");

    return new AgreementStepRevisionId(value);
  }

  public static AgreementStepRevisionId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
