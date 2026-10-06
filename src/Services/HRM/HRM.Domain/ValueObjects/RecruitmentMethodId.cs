public sealed record RecruitmentMethodId : ITypedId<RecruitmentMethodId>
{
  public Guid Value { get; }

  private RecruitmentMethodId(Guid value) => Value = value;

  public static RecruitmentMethodId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Recruitment method id cannot be empty.");

    return new RecruitmentMethodId(value);
  }

  public static RecruitmentMethodId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
