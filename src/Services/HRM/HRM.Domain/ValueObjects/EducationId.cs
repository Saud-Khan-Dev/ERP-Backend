public sealed record EducationId : ITypedId<EducationId>
{
  public Guid Value { get; }

  private EducationId(Guid value) => Value = value;

  public static EducationId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Education record id cannot be empty.");

    return new EducationId(value);
  }

  public static EducationId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
