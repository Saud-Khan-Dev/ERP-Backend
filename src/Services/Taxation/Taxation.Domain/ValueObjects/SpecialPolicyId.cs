public sealed record SpecialPolicyId : ITypedId<SpecialPolicyId>
{
  public Guid Value { get; }

  private SpecialPolicyId(Guid value) => Value = value;

  public static SpecialPolicyId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Special policy id cannot be empty.");

    return new SpecialPolicyId(value);
  }

  public static SpecialPolicyId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
