public sealed record QualificationLevelId : ITypedId<QualificationLevelId>
{
  public Guid Value { get; }

  private QualificationLevelId(Guid value) => Value = value;

  public static QualificationLevelId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Qualification level id cannot be empty.");

    return new QualificationLevelId(value);
  }

  public static QualificationLevelId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
