public sealed record OrganizationUnitId : ITypedId<OrganizationUnitId>
{
  public Guid Value { get; }

  private OrganizationUnitId(Guid value) => Value = value;

  public static OrganizationUnitId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Organization unit id cannot be empty.");

    return new OrganizationUnitId(value);
  }

  public static OrganizationUnitId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
