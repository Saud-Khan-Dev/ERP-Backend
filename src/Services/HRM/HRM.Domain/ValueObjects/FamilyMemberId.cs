public sealed record FamilyMemberId : ITypedId<FamilyMemberId>
{
  public Guid Value { get; }

  private FamilyMemberId(Guid value) => Value = value;

  public static FamilyMemberId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Family member id cannot be empty.");

    return new FamilyMemberId(value);
  }

  public static FamilyMemberId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
