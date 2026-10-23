public sealed record FamilyRelationId : ITypedId<FamilyRelationId>
{
  public Guid Value { get; }

  private FamilyRelationId(Guid value) => Value = value;

  public static FamilyRelationId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Family relation id cannot be empty.");

    return new FamilyRelationId(value);
  }

  public static FamilyRelationId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
